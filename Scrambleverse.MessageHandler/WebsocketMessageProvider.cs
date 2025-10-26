using System.Net.WebSockets;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.IO;
using System.Text;

namespace Scrambleverse.MessageHandler;

/// <summary>
/// Provides WebSocket-based message communication implementing the <see cref="IMessageProvider"/> interface.
/// This class enables real-time, bidirectional communication through WebSocket connections.
/// </summary>
/// <remarks>
/// <para>
/// The WebsocketMessageProvider can work with any WebSocket implementation, including client and server connections.
/// It handles message framing, text encoding/decoding, and connection lifecycle management.
/// </para>
/// <para>
/// Messages are automatically encoded as UTF-8 text when sending and decoded when receiving.
/// The provider supports both single-frame and multi-frame WebSocket messages.
/// </para>
/// <para>
/// Connection management can be configured through the autoClose parameter. When autoClose is true,
/// the underlying WebSocket will be disposed when this provider is disposed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Connect to a WebSocket server
/// var provider = await WebsocketMessageProvider.Connect("ws://localhost:8080/api");
///
/// // Use with existing WebSocket
/// var webSocket = new ClientWebSocket();
/// await webSocket.ConnectAsync(new Uri("ws://example.com"), CancellationToken.None);
/// var provider = new WebsocketMessageProvider(webSocket, autoClose: true);
///
/// // Handle messages
/// provider.OnMessageReceived += async (message) => {
///     Console.WriteLine($"Received: {message}");
/// };
///
/// // Send messages
/// await provider.SendMessage("Hello, WebSocket!");
///
/// // Run message loop
/// await provider.Run();
/// </code>
/// </example>
public class WebsocketMessageProvider(WebSocket webSocket, bool autoClose = false) : IMessageProvider, IDisposable
{
    private readonly WebSocket _webSocket = webSocket;
    private readonly bool _autoClose = autoClose;

    /// <summary>
    /// Creates a new WebSocket connection to the specified URI and returns a WebsocketMessageProvider instance.
    /// </summary>
    /// <param name="uri">
    /// The WebSocket URI to connect to. Should use "ws://" or "wss://" scheme.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the connection attempt.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous connect operation. The task result is a
    /// WebsocketMessageProvider instance with autoClose set to true.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="uri"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="uri"/> is not a valid WebSocket URI.
    /// </exception>
    /// <exception cref="WebSocketException">
    /// Thrown when the WebSocket connection fails.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the cancellation token.
    /// </exception>
    public static async Task<WebsocketMessageProvider> Connect(string uri, CancellationToken? cancellationToken = null)
    {
        return await Connect(new Uri(uri), cancellationToken);
    }

    /// <summary>
    /// Creates a new WebSocket connection to the specified URI and returns a WebsocketMessageProvider instance.
    /// </summary>
    /// <param name="uri">
    /// The WebSocket URI to connect to. Should use "ws://" or "wss://" scheme.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token to cancel the connection attempt.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous connect operation. The task result is a
    /// WebsocketMessageProvider instance with autoClose set to true.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="uri"/> is null.
    /// </exception>
    /// <exception cref="WebSocketException">
    /// Thrown when the WebSocket connection fails.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via the cancellation token.
    /// </exception>
    public static async Task<WebsocketMessageProvider> Connect(Uri uri, CancellationToken? cancellationToken = null)
    {
        var client = new ClientWebSocket();
        await client.ConnectAsync(uri, cancellationToken ?? CancellationToken.None);
        return new WebsocketMessageProvider(client, true);
    }

    /// <summary>
    /// Releases all resources used by the WebsocketMessageProvider.
    /// </summary>
    /// <remarks>
    /// If autoClose was set to true during construction, this method will also dispose
    /// the underlying WebSocket. Otherwise, the WebSocket remains open and must be
    /// managed separately.
    /// </remarks>
    public void Dispose()
    {
        if (_autoClose)
        {
            _webSocket.Dispose();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// This implementation handles WebSocket message framing automatically. Multi-frame messages
    /// are assembled into a single message before triggering the OnMessageReceived event.
    /// Close messages are handled by closing the WebSocket connection gracefully.
    /// </remarks>
    public virtual async Task<bool> NextMessage(ArraySegment<byte>? buffer = null, CancellationToken? cancellationToken = null)
    {
        var _buffer = buffer ?? new ArraySegment<byte>(new byte[1024 * 4]);
        using var ms = new MemoryStream();

        while (true)
        {
            cancellationToken?.ThrowIfCancellationRequested();

            var result = await _webSocket.ReceiveAsync(_buffer, cancellationToken ?? CancellationToken.None);
            ms.Write(_buffer.Array, 0, result.Count);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                return false;
            }

            if (result.EndOfMessage)
            {
                break;
            }
        }

        ms.Seek(0, SeekOrigin.Begin);

        OnMessageReceived?.Invoke(Encoding.UTF8.GetString(ms.ToArray()));

        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This event is raised when a complete text message is received from the WebSocket.
    /// The message content is automatically decoded from UTF-8 bytes to a string.
    /// </remarks>
    protected virtual event Func<string, Task>? OnMessageReceived;

    event Func<string, Task>? IMessageProvider.OnMessageReceived
    {
        add => OnMessageReceived += value;
        remove => OnMessageReceived -= value;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Messages are encoded as UTF-8 and sent as WebSocket text frames. Each call to this method
    /// sends a complete message in a single frame with the EndOfMessage flag set to true.
    /// </remarks>
    protected virtual async Task SendMessage(string message, CancellationToken? cancellationToken = null)
    {
        var messageBuffer = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(messageBuffer);
        await _webSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken ?? CancellationToken.None);
    }

    Task IMessageProvider.SendMessage(string message, CancellationToken? cancellationToken)
        => SendMessage(message, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// This method continuously calls <see cref="NextMessage"/> while the WebSocket connection
    /// remains open. It will exit when the connection is closed, an error occurs, or the
    /// operation is cancelled.
    /// </remarks>
    public async Task Run(ArraySegment<byte>? buffer = null, CancellationToken? cancellationToken = null)
    {
        var _buffer = buffer ?? new ArraySegment<byte>(new byte[1024 * 4]);
        while (_webSocket.State == WebSocketState.Open)
        {
            if (!await NextMessage(_buffer, cancellationToken))
            {
                break;
            }
        }
    }
}
