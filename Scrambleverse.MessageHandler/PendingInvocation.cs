using System.Threading.Tasks;
using System;

namespace Scrambleverse.MessageHandler;

class PendingInvocation<T>
{
    public Type ResultType { get; set; } = typeof(T);

    public TaskCompletionSource<T> TaskCompletionSource { get; set; } = new TaskCompletionSource<T>();

    public PendingInvocationAny ToAny()
    {
        var anyPendingInvocation = new PendingInvocationAny
        {
            ResultType = ResultType,
            TaskCompletionSource = TaskCompletionSource.Cast<T, object?>()
        };
        return anyPendingInvocation;
    }
}

class PendingInvocationAny
{
    public Type ResultType { get; set; } = typeof(object);

    public TaskCompletionSource<object?> TaskCompletionSource { get; set; } = new TaskCompletionSource<object?>();
}
