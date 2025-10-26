using System;
using System.Threading;
using System.Threading.Tasks;

namespace Scrambleverse.MessageHandler;

/// <summary>
/// Provides an interface for sending and receiving messages through various communication protocols.
/// This interface abstracts the underlying message transport mechanism (e.g., WebSocket, TCP, named pipes).
/// </summary>
public interface IMessageProvider : IDisposable
{
    /// <summary>
    /// Occurs when a message is received from the underlying transport.
    /// Subscribers should handle this event to process incoming messages asynchronously.
    /// </summary>
    event Func<string, Task>? OnMessageReceived;

    /// <summary>
    /// Receives the next message from the underlying transport.
    /// This method blocks until a message is available or the connection is closed.
    /// </summary>
    /// <param name="buffer">
    /// Optional buffer to use for receiving data. If not provided, a default buffer will be allocated.
    /// The buffer should be large enough to accommodate the expected message size.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation. If not provided, the operation will not be cancellable.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous receive operation.
    /// The task result is <c>true</c> if a message was received successfully,
    /// or <c>false</c> if the connection was closed or no more messages are available.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the cancellation token.
    /// </exception>
    Task<bool> NextMessage(ArraySegment<byte>? buffer = null, CancellationToken? cancellationToken = null);

    /// <summary>
    /// Continuously processes incoming messages until the connection is closed or an error occurs.
    /// This method will repeatedly call <see cref="NextMessage"/> and raise the <see cref="OnMessageReceived"/> event
    /// for each received message.
    /// </summary>
    /// <param name="buffer">
    /// Optional buffer to use for receiving data. If not provided, a default buffer will be allocated.
    /// This buffer will be reused for all message receives during the run loop.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the operation. If not provided, the operation will continue
    /// until the connection is naturally closed.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous run operation. The task completes when the connection
    /// is closed, an error occurs, or the operation is cancelled.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the cancellation token.
    /// </exception>
    Task Run(ArraySegment<byte>? buffer = null, CancellationToken? cancellationToken = null);

    /// <summary>
    /// Sends a message through the underlying transport.
    /// The message will be encoded and transmitted according to the transport protocol.
    /// </summary>
    /// <param name="message">
    /// The message content to send. This should be a well-formed message according to the
    /// expected protocol (e.g., JSON for API communication).
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the send operation. If not provided, the operation
    /// will not be cancellable.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous send operation. The task completes when the message
    /// has been successfully transmitted or an error occurs.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="message"/> is null.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the cancellation token.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not in a valid state for sending messages.
    /// </exception>
    Task SendMessage(string message, CancellationToken? cancellationToken = null);
}
