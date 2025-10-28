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
    public void Constructor_WithSeparateHandlerObject_RegistersHandlerMethods()
    {
        // Arrange
        var mockProvider = new MockMessageProvider();
        var handlerObject = new SeparateHandlerObject();

        // Act
        var messageHandler = new MessageHandler(mockProvider, handlerObject);

        // Assert
        Assert.True(mockProvider.HasSubscribers);
    }

    [Fact]
    public async Task HandleMessage_WithSeparateHandlerObject_CallsMethodOnHandlerObject()
    {
        // Arrange
        var mockProvider = new MockMessageProvider();
        var handlerObject = new SeparateHandlerObject();
        var messageHandler = new MessageHandler(mockProvider, handlerObject);

        // Act
        var invokeMessage = JsonSerializer.Serialize(new
        {
            type = "invoke",
            id = "test-id",
            name = "ProcessData",
            body = "test data"
        });

        await mockProvider.SimulateMessageReceived(invokeMessage);

        // Assert
        Assert.Equal("test data", handlerObject.ReceivedData);

        // Check result message was sent
        Assert.Single(mockProvider.SentMessages);
        var resultMessage = mockProvider.SentMessages[0];
        var resultJson = JsonNode.Parse(resultMessage);

        Assert.Equal("result", resultJson?["type"]?.ToString());
        Assert.Equal("test-id", resultJson?["id"]?.ToString());
        Assert.Equal("Processed: test data", resultJson?["body"]?.ToString());
    }

    [Fact]
    public async Task HandleMessage_WithSeparateHandlerObjectAndReturnValue_SendsResult()
    {
        // Arrange
        var mockProvider = new MockMessageProvider();
        var handlerObject = new SeparateHandlerObject();
        var messageHandler = new MessageHandler(mockProvider, handlerObject);

        // Act
        var invokeMessage = JsonSerializer.Serialize(new
        {
            type = "invoke",
            id = "calc-id",
            name = "CalculateSum",
            body = new { A = 5, B = 3 }
        });

        await mockProvider.SimulateMessageReceived(invokeMessage);

        // Assert
        Assert.Single(mockProvider.SentMessages);
        var resultMessage = mockProvider.SentMessages[0];
        var resultJson = JsonNode.Parse(resultMessage);

        Assert.Equal("result", resultJson?["type"]?.ToString());
        Assert.Equal("calc-id", resultJson?["id"]?.ToString());
        Assert.Equal(8, resultJson?["body"]?.GetValue<int>());
    }

    [Fact]
    public async Task HandleMessage_WithSeparateHandlerObjectAsync_AwaitsCompletion()
    {
        // Arrange
        var mockProvider = new MockMessageProvider();
        var handlerObject = new SeparateHandlerObject();
        var messageHandler = new MessageHandler(mockProvider, handlerObject);

        // Act
        var invokeMessage = JsonSerializer.Serialize(new
        {
            type = "invoke",
            id = "async-id",
            name = "AsyncOperation",
            body = "test input"
        });

        await mockProvider.SimulateMessageReceived(invokeMessage);

        // Assert
        Assert.Single(mockProvider.SentMessages);
        var resultMessage = mockProvider.SentMessages[0];
        var resultJson = JsonNode.Parse(resultMessage);

        Assert.Equal("result", resultJson?["type"]?.ToString());
        Assert.Equal("async-id", resultJson?["id"]?.ToString());
        Assert.Equal("Async result: test input", resultJson?["body"]?.ToString());
    }

    [Fact]
    public void Constructor_WithNullHandler_UsesCurrentInstanceAsSelf()
    {
        // Arrange
        var mockProvider = new MockMessageProvider();

        // Act
        var handler = new TestMessageHandler(mockProvider);

        // Assert - Should work the same as before (no separate handler object)
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
    public async Task HandleMessage_WithInvokeMessage_CallsMethodHandler()
    {
        // Arrange
        var mockProvider = new MockMessageProvider();
        var handler = new TestMessageHandler(mockProvider);

        // Act
        var invokeMessage = JsonSerializer.Serialize(new
        {
            type = "invoke",
            id = "test-id",
            name = "TestMethod",
            body = "hello world"
        });

        await mockProvider.SimulateMessageReceived(invokeMessage);

        // Assert
        Assert.Equal("hello world", handler.ReceivedMessage);

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

        // Act
        var invokeMessage = JsonSerializer.Serialize(new
        {
            type = "invoke",
            id = "test-id",
            name = "TestMethodWithReturn",
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

        // Act
        var invokeMessage = JsonSerializer.Serialize(new
        {
            type = "invoke",
            id = "test-id",
            name = "ThrowingMethod",
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
            name = "UnknownMethod",
            body = "hello"
        });

        await mockProvider.SimulateMessageReceived(invokeMessage);

        // Assert
        Assert.Empty(mockProvider.SentMessages); // No response
    }

    [Fact]
    public async Task HandleMessage_WithAsyncMethodHandler_AwaitsCompletion()
    {
        // Arrange
        var mockProvider = new MockMessageProvider();
        var handler = new TestMessageHandler(mockProvider);

        // Act
        var invokeMessage = JsonSerializer.Serialize(new
        {
            type = "invoke",
            id = "test-id",
            name = "TestAsyncMethod",
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
        public string? ReceivedMessage { get; private set; }

        public TestMessageHandler(IMessageProvider provider) : base(provider) { }

        [InvocationHandler("TestMethod")]
        public void HandleTestMethod(string message)
        {
            ReceivedMessage = message;
        }

        [InvocationHandler("TestMethodWithReturn")]
        public string HandleTestMethodWithReturn(string message)
        {
            return $"Received: {message}";
        }

        [InvocationHandler("TestAsyncMethod")]
        public async Task<string> HandleTestAsyncMethod(string message)
        {
            await Task.Delay(10);
            return $"Async: {message}";
        }

        [InvocationHandler("ThrowingMethod")]
        public void ThrowingMethod(string message)
        {
            throw new InvalidOperationException("Test exception");
        }
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

    private class SeparateHandlerObject
    {
        public string? ReceivedData { get; private set; }

        [InvocationHandler("ProcessData")]
        public string ProcessData(string data)
        {
            ReceivedData = data;
            return $"Processed: {data}";
        }

        [InvocationHandler("CalculateSum")]
        public int CalculateSum(CalculationRequest request)
        {
            return request.A + request.B;
        }

        [InvocationHandler("AsyncOperation")]
        public async Task<string> AsyncOperation(string input)
        {
            await Task.Delay(1);
            return $"Async result: {input}";
        }
    }

    private class CalculationRequest
    {
        public int A { get; set; }
        public int B { get; set; }
    }
}
