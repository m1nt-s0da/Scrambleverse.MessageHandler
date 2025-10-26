using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Scrambleverse.MessageHandler.Tests;

public class WebsocketMessageProviderTests
{
    [Fact]
    public void Constructor_WithWebSocket_SetsProperties()
    {
        // Arrange
        var mockWebSocket = new MockWebSocket();

        // Act
        var provider = new WebsocketMessageProvider(mockWebSocket, autoClose: false);

        // Assert
        Assert.NotNull(provider);
    }

    [Fact]
    public void Dispose_WithAutoCloseTrue_DisposesWebSocket()
    {
        // Arrange
        var mockWebSocket = new MockWebSocket();
        var provider = new WebsocketMessageProvider(mockWebSocket, autoClose: true);

        // Act
        provider.Dispose();

        // Assert
        Assert.True(mockWebSocket.IsDisposed);
    }

    [Fact]
    public void Dispose_WithAutoCloseFalse_DoesNotDisposeWebSocket()
    {
        // Arrange
        var mockWebSocket = new MockWebSocket();
        var provider = new WebsocketMessageProvider(mockWebSocket, autoClose: false);

        // Act
        provider.Dispose();

        // Assert
        Assert.False(mockWebSocket.IsDisposed);
    }

    [Fact]
    public async Task SendMessage_WithValidMessage_SendsToWebSocket()
    {
        // Arrange
        var mockWebSocket = new MockWebSocket();
        IMessageProvider provider = new WebsocketMessageProvider(mockWebSocket);
        var message = "test message";

        // Act
        await provider.SendMessage(message);

        // Assert
        Assert.Single(mockWebSocket.SentMessages);
        Assert.Equal(message, mockWebSocket.SentMessages[0]);
    }

    [Fact]
    public async Task SendMessage_WithCancellationToken_PassesToWebSocket()
    {
        // Arrange
        var mockWebSocket = new MockWebSocket();
        IMessageProvider provider = new WebsocketMessageProvider(mockWebSocket);
        var cts = new CancellationTokenSource();
        var message = "test message";

        // Act
        await provider.SendMessage(message, cts.Token);

        // Assert
        Assert.Equal(cts.Token, mockWebSocket.LastCancellationToken);
    }

    [Fact]
    public async Task NextMessage_WithCompleteMessage_RaisesOnMessageReceived()
    {
        // Arrange
        var mockWebSocket = new MockWebSocket();
        IMessageProvider provider = new WebsocketMessageProvider(mockWebSocket);
        var receivedMessage = "";

        provider.OnMessageReceived += async (msg) =>
        {
            receivedMessage = msg;
            await Task.CompletedTask;
        };

        var testMessage = "Hello, World!";
        mockWebSocket.SetupReceiveMessage(testMessage);

        // Act
        var result = await provider.NextMessage();

        // Assert
        Assert.True(result);
        Assert.Equal(testMessage, receivedMessage);
    }

    [Fact]
    public async Task NextMessage_WithMultipleFrames_CombinesMessage()
    {
        // Arrange
        var mockWebSocket = new MockWebSocket();
        IMessageProvider provider = new WebsocketMessageProvider(mockWebSocket);
        var receivedMessage = "";

        provider.OnMessageReceived += async (msg) =>
        {
            receivedMessage = msg;
            await Task.CompletedTask;
        };

        // Setup a message that comes in multiple frames
        mockWebSocket.SetupMultiFrameMessage("Hello", " World", "!");

        // Act
        var result = await provider.NextMessage();

        // Assert
        Assert.True(result);
        Assert.Equal("Hello World!", receivedMessage);
    }

    [Fact]
    public async Task Run_WithOpenWebSocket_ProcessesMessages()
    {
        // Arrange
        var mockWebSocket = new MockWebSocket();
        IMessageProvider provider = new WebsocketMessageProvider(mockWebSocket);
        var receivedMessages = new List<string>();

        provider.OnMessageReceived += async (msg) =>
        {
            receivedMessages.Add(msg);
            await Task.CompletedTask;
        };

        // Setup multiple messages followed by a close
        mockWebSocket.SetupSequentialMessages("Message 1", "Message 2", "Message 3");

        // Act
        await provider.Run();

        // Assert
        Assert.Equal(3, receivedMessages.Count);
        Assert.Equal("Message 1", receivedMessages[0]);
        Assert.Equal("Message 2", receivedMessages[1]);
        Assert.Equal("Message 3", receivedMessages[2]);
    }

    [Fact]
    public async Task NextMessage_WithCancellationToken_ThrowsWhenCancelled()
    {
        // Arrange
        var mockWebSocket = new MockWebSocket();
        var provider = new WebsocketMessageProvider(mockWebSocket);
        var cts = new CancellationTokenSource();

        mockWebSocket.SetupCancellation();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.NextMessage(cancellationToken: cts.Token));
    }

    private class MockWebSocket : WebSocket
    {
        public override WebSocketCloseStatus? CloseStatus => _closeStatus;
        public override string? CloseStatusDescription => _closeStatusDescription;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;

        public List<string> SentMessages { get; } = new();
        public CancellationToken LastCancellationToken { get; private set; }
        public bool IsDisposed { get; private set; }
        public bool IsClosed { get; private set; }

        private WebSocketState _state = WebSocketState.Open;
        private WebSocketCloseStatus? _closeStatus;
        private string? _closeStatusDescription;
        private readonly Queue<MockReceiveResult> _receiveQueue = new();
        private bool _shouldThrowCancellation = false;

        public void SetupReceiveMessage(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            _receiveQueue.Enqueue(new MockReceiveResult
            {
                Buffer = bytes,
                MessageType = WebSocketMessageType.Text,
                EndOfMessage = true
            });
        }

        public void SetupMultiFrameMessage(params string[] frames)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                var bytes = Encoding.UTF8.GetBytes(frames[i]);
                _receiveQueue.Enqueue(new MockReceiveResult
                {
                    Buffer = bytes,
                    MessageType = WebSocketMessageType.Text,
                    EndOfMessage = i == frames.Length - 1
                });
            }
        }

        public void SetupSequentialMessages(params string[] messages)
        {
            foreach (var message in messages)
            {
                SetupReceiveMessage(message);
            }
        }

        public void SetupCancellation()
        {
            _shouldThrowCancellation = true;
        }

        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            if (_shouldThrowCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            await Task.Delay(1, cancellationToken); // Simulate async operation

            if (!_receiveQueue.TryDequeue(out var result))
            {
                // No more messages, simulate close
                _state = WebSocketState.Closed;
                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
            }

            // Copy data to buffer
            var bytesToCopy = Math.Min(result.Buffer.Length, buffer.Count);
            Array.Copy(result.Buffer, 0, buffer.Array!, buffer.Offset, bytesToCopy);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _state = WebSocketState.Closed;
            }

            return new WebSocketReceiveResult(bytesToCopy, result.MessageType, result.EndOfMessage);
        }

        public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            LastCancellationToken = cancellationToken;
            await Task.Delay(1, cancellationToken); // Simulate async operation

            var message = Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
            SentMessages.Add(message);
        }

        public override async Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken); // Simulate async operation

            _state = WebSocketState.Closed;
            _closeStatus = closeStatus;
            _closeStatusDescription = statusDescription;
            IsClosed = true;
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override void Abort()
        {
            _state = WebSocketState.Aborted;
        }

        public override void Dispose()
        {
            IsDisposed = true;
            _state = WebSocketState.Closed;
        }

        private class MockReceiveResult
        {
            public byte[] Buffer { get; set; } = Array.Empty<byte>();
            public WebSocketMessageType MessageType { get; set; }
            public bool EndOfMessage { get; set; }
        }
    }
}
