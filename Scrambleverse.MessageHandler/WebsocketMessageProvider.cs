using System.Net.WebSockets;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.IO;
using System.Text;

namespace Scrambleverse.MessageHandler;

public class WebsocketMessageProvider(WebSocket webSocket, bool autoClose = false) : IMessageProvider, IDisposable
{
    private readonly WebSocket _webSocket = webSocket;
    private readonly bool _autoClose = autoClose;

    public static async Task<WebsocketMessageProvider> Connect(string uri, CancellationToken? cancellationToken = null)
    {
        return await Connect(new Uri(uri), cancellationToken);
    }

    public static async Task<WebsocketMessageProvider> Connect(Uri uri, CancellationToken? cancellationToken = null)
    {
        var client = new ClientWebSocket();
        await client.ConnectAsync(uri, cancellationToken ?? CancellationToken.None);
        return new WebsocketMessageProvider(client, true);
    }

    public void Dispose()
    {
        if (_autoClose)
        {
            _webSocket.Dispose();
        }
    }

    public async Task<bool> NextMessage(ArraySegment<byte>? buffer = null, CancellationToken? cancellationToken = null)
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

    public event Func<string, Task>? OnMessageReceived;

    public async Task SendMessage(string message, CancellationToken? cancellationToken = null)
    {
        var messageBuffer = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(messageBuffer);
        await _webSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken ?? CancellationToken.None);
    }

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
