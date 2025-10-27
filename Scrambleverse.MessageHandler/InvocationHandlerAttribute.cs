using System;

namespace Scrambleverse.MessageHandler;

/// <summary>
/// Marks a method as an invocation handler that can be called remotely through the message handler system.
/// This attribute specifies the name that remote callers use to invoke the associated method.
/// </summary>
/// <remarks>
/// <para>
/// Methods decorated with this attribute will be automatically registered in the <see cref="MessageHandler"/>
/// and can be invoked by remote clients by sending invoke messages with the specified name.
/// </para>
/// <para>
/// The method can accept zero or one parameter and can return void, a value, or a Task for asynchronous operations.
/// Methods must be accessible to the MessageHandler instance (public, protected, or private).
/// </para>
/// <example>
/// <code>
/// [InvocationHandler("ProcessData")]
/// public void ProcessData(string data)
/// {
///     // Process the data
/// }
///
/// [InvocationHandler("GetUserAsync")]
/// public async Task&lt;User&gt; GetUserAsync(string userId)
/// {
///     return await userService.GetUserAsync(userId);
/// }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class InvocationHandlerAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the name that remote callers use to invoke this handler.
    /// This name is used in the "name" field of invoke messages sent through the message provider.
    /// </summary>
    /// <value>
    /// The invocation name for this handler. This should be unique within the message handler instance
    /// and follow a consistent naming convention for the application.
    /// </value>
    public string Name { get; } = name;
}
