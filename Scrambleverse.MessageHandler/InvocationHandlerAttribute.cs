using System;

namespace Scrambleverse.MessageHandler;

[AttributeUsage(AttributeTargets.Event, Inherited = false, AllowMultiple = false)]
public class InvocationHandlerAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
