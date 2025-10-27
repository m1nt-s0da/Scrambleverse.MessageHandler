using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Scrambleverse.MessageHandler.Tests;

public class InvocationHandlerInfoTests
{
    [Fact]
    public void Constructor_WithParameterlessMethod_SetsBodyTypeToNull()
    {
        // Arrange
        var testObject = new TestHandler();
        var methodInfo = typeof(TestHandler).GetMethod(nameof(TestHandler.ParameterlessMethod))!;

        // Act
        var handlerInfo = new InvocationHandlerInfo(testObject, methodInfo);

        // Assert
        Assert.Equal(methodInfo, handlerInfo.MethodInfo);
        Assert.Null(handlerInfo.BodyType);
        Assert.Equal(testObject, handlerInfo.Object);
    }

    [Fact]
    public void Constructor_WithSingleParameterMethod_SetsBodyTypeCorrectly()
    {
        // Arrange
        var testObject = new TestHandler();
        var methodInfo = typeof(TestHandler).GetMethod(nameof(TestHandler.SingleParameterMethod))!;

        // Act
        var handlerInfo = new InvocationHandlerInfo(testObject, methodInfo);

        // Assert
        Assert.Equal(methodInfo, handlerInfo.MethodInfo);
        Assert.Equal(typeof(string), handlerInfo.BodyType);
        Assert.Equal(testObject, handlerInfo.Object);
    }

    [Fact]
    public void Constructor_WithMultipleParameterMethod_ThrowsInvalidOperationException()
    {
        // Arrange
        var testObject = new TestHandler();
        var methodInfo = typeof(TestHandler).GetMethod(nameof(TestHandler.MultipleParameterMethod))!;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new InvocationHandlerInfo(testObject, methodInfo));
    }

    [Fact]
    public async Task Invoke_WithParameterlessMethod_ReturnsNullAndExecutesMethod()
    {
        // Arrange
        var testObject = new TestHandler();
        var methodInfo = typeof(TestHandler).GetMethod(nameof(TestHandler.ParameterlessMethod))!;
        var handlerInfo = new InvocationHandlerInfo(testObject, methodInfo);

        // Act
        var result = await handlerInfo.Invoke(null);

        // Assert
        Assert.Null(result);
        Assert.True(testObject.ParameterlessMethodCalled);
    }

    [Fact]
    public async Task Invoke_WithSingleParameterMethod_PassesBodyToMethodAndReturnsNull()
    {
        // Arrange
        var testObject = new TestHandler();
        var methodInfo = typeof(TestHandler).GetMethod(nameof(TestHandler.SingleParameterMethod))!;
        var handlerInfo = new InvocationHandlerInfo(testObject, methodInfo);
        var testBody = "test body";

        // Act
        var result = await handlerInfo.Invoke(testBody);

        // Assert
        Assert.Null(result);
        Assert.Equal(testBody, testObject.ReceivedParameter);
    }

    [Fact]
    public async Task Invoke_WithReturnValueMethod_ReturnsValueFromMethod()
    {
        // Arrange
        var testObject = new TestHandler();
        var methodInfo = typeof(TestHandler).GetMethod(nameof(TestHandler.ReturnValueMethod))!;
        var handlerInfo = new InvocationHandlerInfo(testObject, methodInfo);

        // Act
        var result = await handlerInfo.Invoke(null);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Invoke_WithAsyncMethod_ReturnsAwaitedValue()
    {
        // Arrange
        var testObject = new TestHandler();
        var methodInfo = typeof(TestHandler).GetMethod(nameof(TestHandler.AsyncMethod))!;
        var handlerInfo = new InvocationHandlerInfo(testObject, methodInfo);

        // Act
        var result = await handlerInfo.Invoke(null);

        // Assert
        Assert.Equal("async result", result);
    }

    [Fact]
    public async Task Invoke_WithParameterAndReturnValue_PassesParameterAndReturnsValue()
    {
        // Arrange
        var testObject = new TestHandler();
        var methodInfo = typeof(TestHandler).GetMethod(nameof(TestHandler.ParameterAndReturnMethod))!;
        var handlerInfo = new InvocationHandlerInfo(testObject, methodInfo);
        var testInput = "hello";

        // Act
        var result = await handlerInfo.Invoke(testInput);

        // Assert
        Assert.Equal(5, result);
    }

    private class TestHandler
    {
        public bool ParameterlessMethodCalled { get; private set; }
        public string? ReceivedParameter { get; private set; }

        public void ParameterlessMethod()
        {
            ParameterlessMethodCalled = true;
        }

        public void SingleParameterMethod(string parameter)
        {
            ReceivedParameter = parameter;
        }

        public void MultipleParameterMethod(string param1, int param2)
        {
            // This method should cause an exception in constructor
        }

        public int ReturnValueMethod()
        {
            return 42;
        }

        public async Task<string> AsyncMethod()
        {
            await Task.Delay(1);
            return "async result";
        }

        public int ParameterAndReturnMethod(string input)
        {
            return input?.Length ?? 0;
        }
    }
}
