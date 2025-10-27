using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Collections.ObjectModel;

namespace Scrambleverse.MessageHandler;

/// <summary>
/// Provides a message-based communication system that enables remote procedure calls (RPC) through various transport protocols.
/// This class handles both outgoing invocations to remote endpoints and incoming invocations from remote clients.
/// </summary>
/// <remarks>
/// <para>
/// The MessageHandler automatically discovers and registers methods marked with <see cref="InvocationHandlerAttribute"/>
/// as remotely callable handlers. These handlers can be invoked by remote clients by sending appropriately formatted
/// invoke messages through the underlying <see cref="IMessageProvider"/>.
/// </para>
/// <para>
/// The class supports both synchronous and asynchronous operations, automatically handling the serialization and
/// deserialization of message payloads using System.Text.Json.
/// </para>
/// <para>
/// Message format follows a JSON structure with the following types:
/// <list type="bullet">
/// <item><description><c>invoke</c> - Requests execution of a remote handler</description></item>
/// <item><description><c>result</c> - Returns the result of a successful invocation</description></item>
/// <item><description><c>error</c> - Returns error information when an invocation fails</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Define a message handler with remote callable methods
/// public class MyMessageHandler : MessageHandler
/// {
///     public MyMessageHandler(IMessageProvider provider) : base(provider) { }
///
///     [InvocationHandler("ProcessAsync")]
///     public async Task&lt;string&gt; ProcessDataAsync(string input)
///     {
///         await Task.Delay(100);
///         return $"Processed: {input}";
///     }
/// }
///
/// // Usage
/// var handler = new MyMessageHandler(messageProvider);
///
/// // Call remote handler
/// var result = await handler.Invoke&lt;int, (int, int)&gt;("RemoteCalculate", (5, 3));
/// </code>
/// </example>
public class MessageHandler
{
    private readonly IMessageProvider messageProvider;
    private readonly ReadOnlyDictionary<string, InvocationHandlerInfo> events;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageHandler"/> class with the specified message provider.
    /// </summary>
    /// <param name="messageProvider">
    /// The message provider that handles the underlying communication transport.
    /// This provider will be used for both sending outgoing messages and receiving incoming messages.
    /// </param>
    /// <remarks>
    /// During construction, the handler automatically scans the current type for methods decorated with
    /// <see cref="InvocationHandlerAttribute"/> and registers them as remotely callable handlers.
    /// The message provider's <see cref="IMessageProvider.OnMessageReceived"/> event is subscribed to
    /// for processing incoming messages.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="messageProvider"/> is null.
    /// </exception>
    public MessageHandler(IMessageProvider messageProvider)
    {
        this.messageProvider = messageProvider;
        messageProvider.OnMessageReceived += HandleMessage;

        events = new ReadOnlyDictionary<string, InvocationHandlerInfo>(GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(method => (method, method.GetCustomAttribute<InvocationHandlerAttribute>()))
            .Where(t => t.Item2 != null)
            .ToDictionary(t => t.Item2.Name, t => new InvocationHandlerInfo(this, t.method)));
    }

    /// <summary>
    /// Handles incoming messages from the message provider.
    /// This method is automatically called when the message provider receives a message.
    /// </summary>
    /// <param name="message">
    /// The raw message content as a JSON string. Expected to contain a "type" field
    /// indicating the message type ("result", "invoke", or "error").
    /// </param>
    /// <returns>
    /// A task representing the asynchronous message processing operation.
    /// </returns>
    protected virtual async Task HandleMessage(string message)
    {
        // Handle incoming messages here
        var node = JsonNode.Parse(message);
        if (node != null)
        {
            if (node["type"]?.ToString() == "result")
            {
                ProcessResult(node);
            }

            if (node["type"]?.ToString() == "invoke")
            {
                await ProcessInvocation(node);
            }
        }
    }

    readonly Dictionary<string, PendingInvocationAny> pendingInvokes = [];

    /// <summary>
    /// Invokes a remote handler and waits for the result.
    /// </summary>
    /// <typeparam name="T">
    /// The expected return type of the remote handler.
    /// </typeparam>
    /// <typeparam name="U">
    /// The type of the message body to send to the remote handler.
    /// </typeparam>
    /// <param name="target">
    /// The name of the remote handler to invoke. This should match the name specified
    /// in the <see cref="InvocationHandlerAttribute"/> on the remote endpoint.
    /// </param>
    /// <param name="message">
    /// The message body to send to the remote handler. This will be serialized to JSON
    /// and included in the invoke message.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous invoke operation. The task result contains
    /// the deserialized response from the remote handler.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="target"/> is null.
    /// </exception>
    /// <exception cref="Exception">
    /// Thrown when the remote handler returns an error or when no response is received.
    /// </exception>
    /// <example>
    /// <code>
    /// // Invoke a remote calculator
    /// var result = await handler.Invoke&lt;int, (int, int)&gt;("Calculate", (5, 3));
    ///
    /// // Invoke a remote data processor
    /// var processed = await handler.Invoke&lt;string, string&gt;("ProcessData", "input data");
    /// </code>
    /// </example>
    public virtual async Task<T> Invoke<T, U>(string target, U message)
    {
        var body = new InvokeMessageBody<U>
        {
            Id = Guid.NewGuid().ToString(),
            Name = target,
            Body = message,
        };
        await messageProvider.SendMessage(JsonSerializer.Serialize(body));
        var invoke = new PendingInvocation<T>();
        pendingInvokes[body.Id] = invoke.ToAny();
        return await invoke.TaskCompletionSource.Task;
    }

    /// <summary>
    /// Processes an incoming invocation request and executes the corresponding local handler.
    /// </summary>
    /// <param name="node">
    /// The parsed JSON node containing the invocation request. Expected to have
    /// "name", "id", and optionally "body" fields.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous processing operation.
    /// </returns>
    /// <remarks>
    /// This method looks up the handler by name, deserializes the message body if present,
    /// executes the handler, and sends either a result or error response back through
    /// the message provider.
    /// </remarks>
    protected virtual async Task ProcessInvocation(JsonNode node)
    {
        var name = node["name"]?.ToString();
        var id = node["id"]?.ToString() ?? throw new Exception("Invoke message missing id.");
        if (name != null && events.TryGetValue(name, out var eventInfo))
        {
            var bodyNode = node["body"];
            var body = eventInfo.BodyType != null ? bodyNode?.Deserialize(eventInfo.BodyType) : null;

            object? result;
            try
            {
                result = await eventInfo.Invoke(body);
            }
            catch (Exception ex)
            {
                await messageProvider.SendMessage(JsonSerializer.Serialize(new ErrorMessageBody
                {
                    Id = id,
                    Body = new
                    {
                        type = ex.GetType().FullName,
                        message = ex.Message,
                        stackTrace = ex.StackTrace
                    }
                }));

                goto e;
            }

            await messageProvider.SendMessage(JsonSerializer.Serialize(new ResultMessageBody
            {
                Id = id,
                Body = result
            }));
        e:;
            ;
        }
    }

    /// <summary>
    /// Processes an incoming result message and completes the corresponding pending invocation.
    /// </summary>
    /// <param name="node">
    /// The parsed JSON node containing the result message. Expected to have
    /// "id" and "body" fields.
    /// </param>
    /// <remarks>
    /// This method matches the result with a pending invocation by ID, deserializes the result,
    /// and completes the associated TaskCompletionSource to unblock the waiting Invoke call.
    /// </remarks>
    /// <exception cref="Exception">
    /// Thrown when the result message is missing an ID or when no pending invocation
    /// is found for the given ID.
    /// </exception>
    protected virtual void ProcessResult(JsonNode node)
    {
        var id = node["id"]?.ToString() ?? throw new Exception("Result message missing id.");
        if (pendingInvokes.TryGetValue(id, out var pendingInvoke))
        {
            var resultNode = node["body"];
            if (resultNode != null)
            {
                var result = JsonSerializer.Deserialize(resultNode, pendingInvoke.ResultType);
                pendingInvoke.TaskCompletionSource.SetResult(result);
                pendingInvokes.Remove(id);
            }
        }
        else
        {
            throw new Exception($"No pending invoke found for id {id}");
        }
    }
}
