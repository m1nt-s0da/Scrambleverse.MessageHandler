using System;
using System.Threading;
using System.Threading.Tasks;

namespace Scrambleverse.MessageHandler;

public interface IMessageProvider : IDisposable
{
    event Func<string, Task>? OnMessageReceived;

    Task<bool> NextMessage(ArraySegment<byte>? buffer = null, CancellationToken? cancellationToken = null);

    Task Run(ArraySegment<byte>? buffer = null, CancellationToken? cancellationToken = null);

    Task SendMessage(string message, CancellationToken? cancellationToken = null);
}
