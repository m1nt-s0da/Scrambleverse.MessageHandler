using System;

namespace Scrambleverse.MessageHandler;

/// <summary>
/// Marks an event as an invocation handler that can be called remotely through the message handler system.
/// This attribute specifies the name that remote callers use to invoke the associated event handler.
/// </summary>
/// <remarks>
/// <para>
/// Events decorated with this attribute will be automatically registered in the <see cref="MessageHandler"/>
/// and can be invoked by remote clients by sending invoke messages with the specified name.
/// </para>
/// <para>
/// The event handler can accept zero or one parameter and can return void, a value, or a Task for asynchronous operations.
/// Multiple event handlers for the same event are not supported and will result in an exception.
/// </para>
/// <example>
/// <code>
/// [InvocationHandler("ProcessData")]
/// public event Action&lt;string&gt;? OnProcessData;
///
/// [InvocationHandler("GetUserAsync")]
/// public event Func&lt;string, Task&lt;User&gt;&gt;? OnGetUser;
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Event, Inherited = false, AllowMultiple = false)]
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
