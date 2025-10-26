using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Collections.ObjectModel;

namespace Scrambleverse.MessageHandler;

public class MessageHandler
{
    private readonly IMessageProvider messageProvider;
    private readonly ReadOnlyDictionary<string, InvocationHandlerInfo> events;

    public MessageHandler(IMessageProvider messageProvider)
    {
        this.messageProvider = messageProvider;
        messageProvider.OnMessageReceived += HandleMessage;

        events = new ReadOnlyDictionary<string, InvocationHandlerInfo>(GetType().GetEvents(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(evt => (evt, evt.GetCustomAttribute<InvocationHandlerAttribute>()))
            .Where(t => t.Item2 != null)
            .ToDictionary(t => t.Item2.Name, t => InvocationHandlerInfo.FromMessageHandler(this, t.evt)));
    }

    private async Task HandleMessage(string message)
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

    public async Task<T> Invoke<T, U>(string target, U message)
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

    private async Task ProcessInvocation(JsonNode node)
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
                    Body = new {
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

    private void ProcessResult(JsonNode node)
    {
        var id = node["id"]?.ToString() ?? throw new Exception("Result message missing id.");
        if (pendingInvokes.TryGetValue(id, out var pendingInvoke))
        {
            var resultNode = node["body"];
            if (resultNode != null)
            {
                var result = JsonSerializer.Deserialize(resultNode, pendingInvoke.ResultType);
                // var taskCompletionSourceType = typeof(TaskCompletionSource<>).MakeGenericType(pendingInvoke.ResultType);
                // var setResultMethod = taskCompletionSourceType.GetMethod("SetResult");
                pendingInvoke.TaskCompletionSource.SetResult(result);
                // setResultMethod?.Invoke(pendingInvoke.TaskCompletionSource, [result]);
                pendingInvokes.Remove(id);
            }
        } else
        {
            throw new Exception($"No pending invoke found for id {id}");
        }
    }
}
