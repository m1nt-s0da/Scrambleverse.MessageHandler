using System;
using System.Threading.Tasks;
using Scrambleverse.MessageHandler.Reflection;
using Xunit;

namespace Scrambleverse.MessageHandler.Tests;

public class AwaitableTests
{
    [Fact]
    public void GetAwaitedResult_WithTaskOfInt_ReturnsIntType()
    {
        // Arrange
        var taskType = typeof(Task<int>);

        // Act
        var result = taskType.GetAwaitedResult(out var awaitedResultType);

        // Assert
        Assert.True(result);
        Assert.Equal(typeof(int), awaitedResultType);
    }

    [Fact]
    public void GetAwaitedResult_WithTaskOfString_ReturnsStringType()
    {
        // Arrange
        var taskType = typeof(Task<string>);

        // Act
        var result = taskType.GetAwaitedResult(out var awaitedResultType);

        // Assert
        Assert.True(result);
        Assert.Equal(typeof(string), awaitedResultType);
    }

    [Fact]
    public void GetAwaitedResult_WithTask_ReturnsVoidType()
    {
        // Arrange
        var taskType = typeof(Task);

        // Act
        var result = taskType.GetAwaitedResult(out var awaitedResultType);

        // Assert
        Assert.True(result);
        Assert.Equal(typeof(void), awaitedResultType);
    }

    [Fact]
    public void GetAwaitedResult_WithValueTaskOfInt_ReturnsIntType()
    {
        // Arrange
        var valueTaskType = typeof(ValueTask<int>);

        // Act
        var result = valueTaskType.GetAwaitedResult(out var awaitedResultType);

        // Assert
        Assert.True(result);
        Assert.Equal(typeof(int), awaitedResultType);
    }

    [Fact]
    public void GetAwaitedResult_WithValueTask_ReturnsVoidType()
    {
        // Arrange
        var valueTaskType = typeof(ValueTask);

        // Act
        var result = valueTaskType.GetAwaitedResult(out var awaitedResultType);

        // Assert
        Assert.True(result);
        Assert.Equal(typeof(void), awaitedResultType);
    }

    [Fact]
    public void GetAwaitedResult_WithNonAwaitableType_ReturnsFalse()
    {
        // Arrange
        var nonAwaitableType = typeof(int);

        // Act
        var result = nonAwaitableType.GetAwaitedResult(out var awaitedResultType);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetAwaitedResult_WithTaskOfCustomType_ReturnsCustomType()
    {
        // Arrange
        var taskType = typeof(Task<TestClass>);

        // Act
        var result = taskType.GetAwaitedResult(out var awaitedResultType);

        // Assert
        Assert.True(result);
        Assert.Equal(typeof(TestClass), awaitedResultType);
    }

    [Fact]
    public async Task AwaitIfAwaitable_WithTaskOfInt_ReturnsAwaitedValue()
    {
        // Arrange
        var task = Task.FromResult(42);

        // Act
        var result = await task.AwaitIfAwaitable();

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task AwaitIfAwaitable_WithTaskOfString_ReturnsAwaitedValue()
    {
        // Arrange
        var task = Task.FromResult("Hello, World!");

        // Act
        var result = await task.AwaitIfAwaitable();

        // Assert
        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public async Task AwaitIfAwaitable_WithTask_ReturnsNull()
    {
        // Arrange
        var task = Task.FromResult<object?>(null);

        // Act
        var result = await task.AwaitIfAwaitable();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AwaitIfAwaitable_WithValueTaskOfInt_ReturnsAwaitedValue()
    {
        // Arrange
        var valueTask = new ValueTask<int>(123);

        // Act
        var result = await valueTask.AwaitIfAwaitable();

        // Assert
        Assert.Equal(123, result);
    }

    [Fact]
    public async Task AwaitIfAwaitable_WithValueTask_ReturnsNull()
    {
        // Arrange
        var valueTask = new ValueTask();

        // Act
        var result = await valueTask.AwaitIfAwaitable();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AwaitIfAwaitable_WithNonAwaitableObject_ReturnsOriginalObject()
    {
        // Arrange
        var nonAwaitableObject = "Not awaitable";

        // Act
        var result = await nonAwaitableObject.AwaitIfAwaitable();

        // Assert
        Assert.Equal("Not awaitable", result);
    }

    [Fact]
    public async Task AwaitIfAwaitable_WithNullObject_ReturnsNull()
    {
        // Arrange
        object? nullObject = null;

        // Act
        var result = await nullObject!.AwaitIfAwaitable();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AwaitIfAwaitable_WithTaskOfCustomType_ReturnsAwaitedValue()
    {
        // Arrange
        var customObject = new TestClass();
        var task = Task.FromResult(customObject);

        // Act
        var result = await task.AwaitIfAwaitable();

        // Assert
        Assert.Same(customObject, result);
    }

    private class TestClass { }
}
