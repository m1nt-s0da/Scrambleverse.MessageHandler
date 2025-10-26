using System;
using System.Threading.Tasks;

namespace Scrambleverse.MessageHandler;

internal static class TaskCast
{
    public static Task<U> Cast<T, U>(this Task<T> task)
    {
        return task.ContinueWith(t => (U)(object)t.Result!);
    }

    public static TaskCompletionSource<U> Cast<T, U>(this TaskCompletionSource<T> tcs)
    {
        var newTcs = new TaskCompletionSource<U>();
        tcs.Task.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                newTcs.TrySetException(t.Exception);
            }
            else if (t.IsCanceled)
            {
                newTcs.TrySetCanceled();
            }
            else
            {
                newTcs.TrySetResult((U)(object)t.Result!);
            }
        });
        newTcs.Task.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                tcs.TrySetException(t.Exception);
            }
            else if (t.IsCanceled)
            {
                tcs.TrySetCanceled();
            }
            else
            {
                tcs.TrySetResult((T)(object)t.Result!);
            }
        });
        return newTcs;
    }
}
