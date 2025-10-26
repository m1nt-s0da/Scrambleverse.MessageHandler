using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Scrambleverse.MessageHandler.Tests;

public class InvocationHandlerInfoTests
{
    [Fact]
    public void Constructor_WithParameterlessEventHandler_SetsBodyTypeToNull()
    {
        // Arrange
        InvocationHandlerInfo.RaiseEventDelegate raiseDelegate = (args) => { return null; };
        var eventInfo = CreateEventInfo(typeof(Action));

        // Act
        var handlerInfo = new InvocationHandlerInfo(raiseDelegate, eventInfo);

        // Assert
        Assert.Equal(eventInfo, handlerInfo.EventInfo);
        Assert.Null(handlerInfo.BodyType);
        Assert.Equal(raiseDelegate, handlerInfo.RaiseDelegate);
        Assert.NotNull(handlerInfo.InvokeMethod);
    }

    [Fact]
    public void Constructor_WithSingleParameterEventHandler_SetsBodyTypeCorrectly()
    {
        // Arrange
        string? receivedValue = null;
        InvocationHandlerInfo.RaiseEventDelegate raiseDelegate = (args) =>
        {
            receivedValue = args[0] as string;
            return null;
        };
        var eventInfo = CreateEventInfo(typeof(Action<string>));

        // Act
        var handlerInfo = new InvocationHandlerInfo(raiseDelegate, eventInfo);

        // Assert
        Assert.Equal(eventInfo, handlerInfo.EventInfo);
        Assert.Equal(typeof(string), handlerInfo.BodyType);
        Assert.Equal(raiseDelegate, handlerInfo.RaiseDelegate);
        Assert.NotNull(handlerInfo.InvokeMethod);
    }

    [Fact]
    public void Constructor_WithMultipleParameterEventHandler_ThrowsInvalidOperationException()
    {
        // Arrange
        InvocationHandlerInfo.RaiseEventDelegate raiseDelegate = (args) => null;
        var eventInfo = CreateEventInfo(typeof(Action<string, int>));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new InvocationHandlerInfo(raiseDelegate, eventInfo));
    }

    [Fact]
    public async Task Invoke_WithParameterlessHandler_ReturnsNullAndExecutesHandler()
    {
        // Arrange
        bool executed = false;
        InvocationHandlerInfo.RaiseEventDelegate raiseDelegate = (args) =>
        {
            executed = true;
            return null;
        };
        var eventInfo = CreateEventInfo(typeof(Action));
        var handlerInfo = new InvocationHandlerInfo(raiseDelegate, eventInfo);

        // Act
        var result = await handlerInfo.Invoke(null);

        // Assert
        Assert.Null(result);
        Assert.True(executed);
    }

    [Fact]
    public async Task Invoke_WithSingleParameterHandler_PassesBodyToHandlerAndReturnsNull()
    {
        // Arrange
        string? receivedBody = null;
        InvocationHandlerInfo.RaiseEventDelegate raiseDelegate = (args) =>
        {
            receivedBody = args[0] as string;
            return null;
        };
        var eventInfo = CreateEventInfo(typeof(Action<string>));
        var handlerInfo = new InvocationHandlerInfo(raiseDelegate, eventInfo);
        var testBody = "test body";

        // Act
        var result = await handlerInfo.Invoke(testBody);

        // Assert
        Assert.Null(result);
        Assert.Equal(testBody, receivedBody);
    }

    [Fact]
    public async Task Invoke_WithReturnValueHandler_ReturnsValueFromHandler()
    {
        // Arrange
        InvocationHandlerInfo.RaiseEventDelegate raiseDelegate = (args) => 42;
        var eventInfo = CreateEventInfo(typeof(Func<int>));
        var handlerInfo = new InvocationHandlerInfo(raiseDelegate, eventInfo);

        // Act
        var result = await handlerInfo.Invoke(null);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Invoke_WithAsyncHandler_ReturnsAwaitedValue()
    {
        // Arrange
        InvocationHandlerInfo.RaiseEventDelegate raiseDelegate = (args) =>
        {
            return Task.Run(async () =>
            {
                await Task.Delay(1);
                return "async result";
            });
        };
        var eventInfo = CreateEventInfo(typeof(Func<Task<string>>));
        var handlerInfo = new InvocationHandlerInfo(raiseDelegate, eventInfo);

        // Act
        var result = await handlerInfo.Invoke(null);

        // Assert
        Assert.Equal("async result", result);
    }

    // Note: Multiple handlers test is no longer applicable with the new RaiseEventDelegate approach
    // as the delegate handling is now managed within the FromMessageHandler method

    [Fact]
    public async Task Invoke_WithParameterAndReturnValue_PassesParameterAndReturnsValue()
    {
        // Arrange
        InvocationHandlerInfo.RaiseEventDelegate raiseDelegate = (args) =>
        {
            var input = args[0] as string;
            return input?.Length ?? 0;
        };
        var eventInfo = CreateEventInfo(typeof(Func<string, int>));
        var handlerInfo = new InvocationHandlerInfo(raiseDelegate, eventInfo);
        var testInput = "hello";

        // Act
        var result = await handlerInfo.Invoke(testInput);

        // Assert
        Assert.Equal(5, result);
    }

    private static EventInfo CreateEventInfo(Type delegateType)
    {
        // Create a mock EventInfo for testing
        var eventInfo = typeof(TestEventClass).GetEvent(nameof(TestEventClass.TestEvent))!;

        // We need to replace the EventHandlerType to match our test delegate
        var mockEventInfo = new MockEventInfo(eventInfo, delegateType);
        return mockEventInfo;
    }

    private class TestEventClass
    {
        public event Action? TestEvent;
    }

    private class MockEventInfo : EventInfo
    {
        private readonly EventInfo _baseEventInfo;
        private readonly Type _eventHandlerType;

        public MockEventInfo(EventInfo baseEventInfo, Type eventHandlerType)
        {
            _baseEventInfo = baseEventInfo;
            _eventHandlerType = eventHandlerType;
        }

        public override Type EventHandlerType => _eventHandlerType;
        public override string Name => _baseEventInfo.Name;
        public override Type? DeclaringType => _baseEventInfo.DeclaringType;
        public override Type? ReflectedType => _baseEventInfo.ReflectedType;
        public override EventAttributes Attributes => _baseEventInfo.Attributes;

        public override MethodInfo? GetAddMethod(bool nonPublic) => _baseEventInfo.GetAddMethod(nonPublic);
        public override MethodInfo? GetRemoveMethod(bool nonPublic) => _baseEventInfo.GetRemoveMethod(nonPublic);
        public override MethodInfo? GetRaiseMethod(bool nonPublic) => _baseEventInfo.GetRaiseMethod(nonPublic);
        public override object[] GetCustomAttributes(bool inherit) => _baseEventInfo.GetCustomAttributes(inherit);
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => _baseEventInfo.GetCustomAttributes(attributeType, inherit);
        public override bool IsDefined(Type attributeType, bool inherit) => _baseEventInfo.IsDefined(attributeType, inherit);
    }
}
