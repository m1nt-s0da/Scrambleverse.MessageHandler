using System;
using System.Reflection;
using System.Threading.Tasks;
using Scrambleverse.MessageHandler.Reflection;

namespace Scrambleverse.MessageHandler;

public class InvocationHandlerInfo
{
    public delegate object? RaiseEventDelegate(params object?[] args);

    public EventInfo EventInfo { get; }
    public Type? BodyType { get; }
    public MethodInfo InvokeMethod { get; }
    public RaiseEventDelegate RaiseDelegate { get; }

    public InvocationHandlerInfo(RaiseEventDelegate raise, EventInfo eventInfo)
    {
        EventInfo = eventInfo;
        RaiseDelegate = raise;

        InvokeMethod = eventInfo.EventHandlerType.GetMethod("Invoke")!;

        var @params = InvokeMethod.GetParameters();
        if (@params.Length == 0)
        {
            BodyType = null;
        }
        else if (@params.Length == 1)
        {
            BodyType = @params[0].ParameterType;
        }
        else
        {
            throw new InvalidOperationException("Event handler has more than one parameter");
        }
    }

    public static InvocationHandlerInfo FromMessageHandler(MessageHandler handler, EventInfo eventInfo)
    {
        var raiseField = handler.GetType().GetField(eventInfo.Name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Could not find event backing field");
        return new InvocationHandlerInfo(args =>
        {
            var raise = (Delegate)raiseField.GetValue(handler);
            var handlers = raise.GetInvocationList();
            if (handlers.Length != 1)
            {
                throw new InvalidOperationException("Multiple handlers are not supported");
            }
            return handlers[0].DynamicInvoke(args);
        }, eventInfo);
    }

    public async Task<object?> Invoke(object? body)
    {
        object?[] args = BodyType != null ? [body] : [];
        var result = RaiseDelegate.DynamicInvoke([args]);
        return await result.AwaitIfAwaitable();
    }
}
