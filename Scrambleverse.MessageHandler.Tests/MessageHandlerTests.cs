using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Scrambleverse.MessageHandler.Tests;

public class MessageHandlerTests
{
    [Fact]
    public void Constructor_WithMessageProvider_SubscribesToOnMessageReceived()
    {
        // Arrange
        var mockProvider = new MockMessageProvider();

        // Act
        var handler = new TestMessageHandler(mockProvider);

        // Assert
        Assert.True(mockProvider.HasSubscribers);
    }

    [Fact]
    public async Task Invoke_WithValidTargetAndMessage_SendsInvokeMessage()
    {
        // Arrange
        var mockProvider = new MockMessageProvider();
        var handler = new TestMessageHandler(mockProvider);
        var target = "testTarget";
        var message = "test message";

        // Act
        var invokeTask = handler.Invoke<string, string>(target, message);

        // Assert sent message
        Assert.Single(mockProvider.SentMessages);
        var sentMessage = mockProvider.SentMessages[0];
        var sentJson = JsonNode.Parse(sentMessage);

        Assert.Equal("invoke", sentJson?["type"]?.ToString());
        Assert.Equal(target, sentJson?["name"]?.ToString());
        Assert.Equal(message, sentJson?["body"]?.ToString());
        Assert.NotNull(sentJson?["id"]?.ToString());

        // Complete the invoke
        var id = sentJson?["id"]?.ToString();
        var resultMessage = JsonSerializer.Serialize(new
        {
            type = "result",
            id = id,
            body = "response"
        });

        await mockProvider.SimulateMessageReceived(resultMessage);
        var result = await invokeTask;

        Assert.Equal("response", result);
    }

    [Fact]
    public async Task HandleMessage_WithResultMessage_CompletesInvocation()
    {
        // Arrange
        var mockProvider = new MockMessageProvider();
        var handler = new TestMessageHandler(mockProvider);

        // Start an invocation
        var invokeTask = handler.Invoke<int, string>("target", "message");
        var sentMessage = mockProvider.SentMessages[0];
        var sentJson = JsonNode.Parse(sentMessage);
        var id = sentJson?["id"]?.ToString();

        // Act - simulate result message
        var resultMessage = JsonSerializer.Serialize(new
        {
            type = "result",
            id = id,
            body = 42
        });

        await mockProvider.SimulateMessageReceived(resultMessage);
        var result = await invokeTask;

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task HandleMessage_WithInvalidResultId_ThrowsException()
    {
        // Arrange
        var mockProvider = new MockMessageProvider();
        var handler = new TestMessageHandler(mockProvider);

        // Act & Assert
        var resultMessage = JsonSerializer.Serialize(new
        {
            type = "result",
            id = "invalid-id",
            body = 42
        });

        await Assert.ThrowsAsync<Exception>(() => mockProvider.SimulateMessageReceived(resultMessage));
    }

    [Fact]
    public async Task HandleMessage_WithInvokeMessage_CallsEventHandler()
    {
        // Arrange
        var mockProvider = new MockMessageProvider();
        var handler = new TestMessageHandler(mockProvider);

        string? receivedMessage = null;
        handler.TestEvent += (msg) => receivedMessage = msg;

        // Act
        var invokeMessage = JsonSerializer.Serialize(new
        {
            type = "invoke",
            id = "test-id",
            name = "TestEvent",
            body = "hello world"
        });

        await mockProvider.SimulateMessageReceived(invokeMessage);

        // Assert
        Assert.Equal("hello world", receivedMessage);

        // Check result message was sent
        Assert.Single(mockProvider.SentMessages); // result
        var resultMessage = mockProvider.SentMessages[0];
        var resultJson = JsonNode.Parse(resultMessage);

        Assert.Equal("result", resultJson?["type"]?.ToString());
        Assert.Equal("test-id", resultJson?["id"]?.ToString());
        Assert.Null(resultJson?["body"]);
    }

    [Fact]
    public async Task HandleMessage_WithInvokeMessageAndReturnValue_SendsResultWithValue()
    {
        // Arrange
        var mockProvider = new MockMessageProvider();
        var handler = new TestMessageHandler(mockProvider);

        handler.TestEventWithReturn += (msg) => $"Received: {msg}";

        // Act
        var invokeMessage = JsonSerializer.Serialize(new
        {
            type = "invoke",
            id = "test-id",
            name = "TestEventWithReturn",
            body = "hello"
        });

        await mockProvider.SimulateMessageReceived(invokeMessage);

        // Assert
        Assert.Single(mockProvider.SentMessages);
        var resultMessage = mockProvider.SentMessages[0];
        var resultJson = JsonNode.Parse(resultMessage);

        Assert.Equal("result", resultJson?["type"]?.ToString());
        Assert.Equal("test-id", resultJson?["id"]?.ToString());
        Assert.Equal("Received: hello", resultJson?["body"]?.ToString());
    }

    [Fact]
    public async Task HandleMessage_WithInvokeMessageAndException_SendsErrorMessage()
    {
        // Arrange
        var mockProvider = new MockMessageProvider();
        var handler = new TestMessageHandler(mockProvider);

        handler.TestEvent += (msg) => throw new InvalidOperationException("Test exception");

        // Act
        var invokeMessage = JsonSerializer.Serialize(new
        {
            type = "invoke",
            id = "test-id",
            name = "TestEvent",
            body = "hello"
        });

        await mockProvider.SimulateMessageReceived(invokeMessage);

        // Assert
        Assert.Single(mockProvider.SentMessages);
        var errorMessage = mockProvider.SentMessages[0];
        var errorJson = JsonNode.Parse(errorMessage);

        Assert.Equal("error", errorJson?["type"]?.ToString());
        Assert.Equal("test-id", errorJson?["id"]?.ToString());
        Assert.NotNull(errorJson?["body"]);
    }

    [Fact]
    public async Task HandleMessage_WithUnknownInvokeName_DoesNotSendResponse()
    {
        // Arrange
        var mockProvider = new MockMessageProvider();
        var handler = new TestMessageHandler(mockProvider);

        // Act
        var invokeMessage = JsonSerializer.Serialize(new
        {
            type = "invoke",
            id = "test-id",
            name = "UnknownEvent",
            body = "hello"
        });

        await mockProvider.SimulateMessageReceived(invokeMessage);

        // Assert
        Assert.Empty(mockProvider.SentMessages); // Only the original message, no response
    }

    [Fact]
    public async Task HandleMessage_WithAsyncEventHandler_AwaitsCompletion()
    {
        // Arrange
        var mockProvider = new MockMessageProvider();
        var handler = new TestMessageHandler(mockProvider);

        handler.TestAsyncEvent += async (msg) =>
        {
            await Task.Delay(10);
            return $"Async: {msg}";
        };

        // Act
        var invokeMessage = JsonSerializer.Serialize(new
        {
            type = "invoke",
            id = "test-id",
            name = "TestAsyncEvent",
            body = "hello"
        });

        await mockProvider.SimulateMessageReceived(invokeMessage);

        // Assert
        Assert.Single(mockProvider.SentMessages);
        var resultMessage = mockProvider.SentMessages[0];
        var resultJson = JsonNode.Parse(resultMessage);

        Assert.Equal("result", resultJson?["type"]?.ToString());
        Assert.Equal("Async: hello", resultJson?["body"]?.ToString());
    }

    private class TestMessageHandler : MessageHandler
    {
        public TestMessageHandler(IMessageProvider provider) : base(provider) { }

        [InvocationHandler("TestEvent")]
        public event Action<string>? TestEvent;

        [InvocationHandler("TestEventWithReturn")]
        public event Func<string, string>? TestEventWithReturn;

        [InvocationHandler("TestAsyncEvent")]
        public event Func<string, Task<string>>? TestAsyncEvent;
    }

    private class MockMessageProvider : IMessageProvider
    {
        private event Func<string, Task>? _onMessageReceived;

        public event Func<string, Task>? OnMessageReceived
        {
            add { _onMessageReceived += value; }
            remove { _onMessageReceived -= value; }
        }

        public bool HasSubscribers => _onMessageReceived != null;
        public List<string> SentMessages { get; } = new();

        public async Task SimulateMessageReceived(string message)
        {
            if (_onMessageReceived != null)
            {
                await _onMessageReceived(message);
            }
        }

        public Task<bool> NextMessage(ArraySegment<byte>? buffer = null, CancellationToken? cancellationToken = null)
        {
            throw new NotImplementedException();
        }

        public Task Run(ArraySegment<byte>? buffer = null, CancellationToken? cancellationToken = null)
        {
            throw new NotImplementedException();
        }

        public Task SendMessage(string message, CancellationToken? cancellationToken = null)
        {
            SentMessages.Add(message);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            // No cleanup needed for mock
        }
    }
}
