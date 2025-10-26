using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Scrambleverse.MessageHandler.Reflection;

/// <summary>
/// Provides utility methods for working with awaitable objects and types through reflection.
/// This class enables dynamic awaiting of objects that implement the awaitable pattern,
/// regardless of their specific type.
/// </summary>
/// <remarks>
/// <para>
/// The awaitable pattern in .NET requires an object to have:
/// <list type="bullet">
/// <item><description>A GetAwaiter() method that returns an awaiter</description></item>
/// <item><description>The awaiter must have an IsCompleted property</description></item>
/// <item><description>The awaiter must have OnCompleted(Action) or UnsafeOnCompleted(Action) method</description></item>
/// <item><description>The awaiter must have a GetResult() method</description></item>
/// </list>
/// </para>
/// <para>
/// This class provides reflection-based utilities to work with such objects dynamically,
/// allowing code to await objects without knowing their compile-time type.
/// </para>
/// </remarks>
public static class Awaitable
{
    /// <summary>
    /// Awaits an object if it implements the awaitable pattern, otherwise returns the object as-is.
    /// </summary>
    /// <param name="obj">
    /// The object to potentially await. Can be any object, including null, awaitable types
    /// (Task, ValueTask, custom awaitables), or non-awaitable types.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is:
    /// <list type="bullet">
    /// <item><description>null if the input object is null</description></item>
    /// <item><description>The awaited result if the object is awaitable</description></item>
    /// <item><description>The original object if it's not awaitable</description></item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method uses reflection to determine if an object implements the awaitable pattern.
    /// If the object is awaitable, it will be dynamically awaited and the result returned.
    /// If the object is not awaitable, it is returned unchanged.
    /// </para>
    /// <para>
    /// This is particularly useful in scenarios where you receive objects of unknown types
    /// that might be awaitable (such as method invocation results that could be Task&lt;T&gt; or T).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // With a Task
    /// var task = Task.FromResult(42);
    /// var result = await task.AwaitIfAwaitable(); // Returns 42
    ///
    /// // With a non-awaitable object
    /// var value = "Hello";
    /// var result = await value.AwaitIfAwaitable(); // Returns "Hello"
    ///
    /// // With null
    /// object nullObj = null;
    /// var result = await nullObj.AwaitIfAwaitable(); // Returns null
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the object appears to be awaitable but GetAwaiter() returns null.
    /// </exception>
    public static async Task<object?> AwaitIfAwaitable(this object obj)
    {
        if (obj == null)
        {
            return null;
        }

        var type = obj.GetType();
        if (type.GetAwaitedResult(out var _))
        {
            GetAwaiterMethod(type, out var awaiterMethod);
            var awaiter = awaiterMethod.Invoke(obj, null) ?? throw new InvalidOperationException("Failed to get awaiter.");
            var awaiterType = awaiter!.GetType();

            GetIsCompletedProperty(awaiterType, out var isCompletedProperty);
            while (true)
            {
                var isCompleted = (bool)isCompletedProperty!.GetValue(awaiter)!;
                if (isCompleted)
                {
                    var getResultMethod = awaiterType.GetMethod("GetResult");
                    return getResultMethod!.Invoke(awaiter, null);
                }

                await Task.Yield();
            }
        }
        else
        {
            return obj;
        }
    }

    /// <summary>
    /// Determines if a type implements the awaitable pattern and extracts the awaited result type.
    /// </summary>
    /// <param name="type">
    /// The type to examine for the awaitable pattern.
    /// </param>
    /// <param name="awaitedResultType">
    /// When this method returns true, contains the type that would be returned when awaiting
    /// an instance of the input type. When this method returns false, this parameter is undefined.
    /// </param>
    /// <returns>
    /// true if the type implements the awaitable pattern; otherwise, false.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method checks if a type follows the awaitable pattern by verifying:
    /// <list type="number">
    /// <item><description>The type has a parameterless GetAwaiter() method</description></item>
    /// <item><description>The awaiter type has an IsCompleted property of type bool</description></item>
    /// <item><description>The awaiter type has either OnCompleted(Action) or UnsafeOnCompleted(Action) method</description></item>
    /// <item><description>The awaiter type has a GetResult() method</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// For standard .NET types:
    /// <list type="bullet">
    /// <item><description>Task returns typeof(void)</description></item>
    /// <item><description>Task&lt;T&gt; returns typeof(T)</description></item>
    /// <item><description>ValueTask returns typeof(void)</description></item>
    /// <item><description>ValueTask&lt;T&gt; returns typeof(T)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Check Task&lt;int&gt;
    /// if (typeof(Task&lt;int&gt;).GetAwaitedResult(out var resultType))
    /// {
    ///     Console.WriteLine(resultType); // Prints: System.Int32
    /// }
    ///
    /// // Check non-awaitable type
    /// if (typeof(string).GetAwaitedResult(out var stringType))
    /// {
    ///     // This block won't execute
    /// }
    /// else
    /// {
    ///     Console.WriteLine("String is not awaitable");
    /// }
    /// </code>
    /// </example>
    public static bool GetAwaitedResult(this Type type, out Type awaitedResultType)
    {
        if (GetAwaiterMethod(type, out var awaiterMethod) && GetReturnType(awaiterMethod, out var awaiter))
        {
            if (GetIsCompletedProperty(awaiter, out _) && (GetOnCompletedMethod(awaiter, out _) || GetUnsafeOnCompletedMethod(awaiter, out _)) && GetGetResultMethod(awaiter, out var getResultMethod))
            {
                var returnType = getResultMethod.ReturnType;
                if (returnType != null)
                {
                    awaitedResultType = returnType;
                    return true;
                }
            }
        }
        awaitedResultType = default!;
        return false;
    }

    static bool GetAwaiterMethod(Type type, out MethodInfo getAwaiterMethod)
    {
        getAwaiterMethod = type.GetMethod("GetAwaiter", Type.EmptyTypes);
        return getAwaiterMethod != null;
    }

    static bool GetReturnType(MethodInfo methodInfo, out Type returnType)
    {
        returnType = methodInfo.ReturnType;
        return returnType != null;
    }

    static bool GetIsCompletedProperty(Type awaiterType, out PropertyInfo isCompletedProperty)
    {
        var property = awaiterType.GetProperty("IsCompleted");
        if (property == null || property.PropertyType != typeof(bool))
        {
            isCompletedProperty = default!;
            return false;
        }
        isCompletedProperty = property;
        return true;
    }

    static bool GetOnCompletedMethod(Type awaiterType, out MethodInfo onCompletedMethod)
    {
        onCompletedMethod = awaiterType.GetMethod("OnCompleted", [typeof(Action)]);
        return onCompletedMethod != null;
    }

    static bool GetUnsafeOnCompletedMethod(Type awaiterType, out MethodInfo unsafeOnCompletedMethod)
    {
        unsafeOnCompletedMethod = awaiterType.GetMethod("UnsafeOnCompleted", [typeof(Action)]);
        return unsafeOnCompletedMethod != null;
    }

    static bool GetGetResultMethod(Type awaiterType, out MethodInfo getResultMethod)
    {
        getResultMethod = awaiterType.GetMethod("GetResult");
        return getResultMethod != null;
    }
}

