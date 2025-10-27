using System;
using System.Reflection;
using System.Threading.Tasks;
using Scrambleverse.MessageHandler.Reflection;

namespace Scrambleverse.MessageHandler;

/// <summary>
/// Encapsulates information about a method that can be invoked remotely through the message handler system.
/// This class provides metadata about the method signature and handles the actual method invocation.
/// </summary>
/// <remarks>
/// <para>
/// This class is used internally by <see cref="MessageHandler"/> to store information about methods
/// decorated with <see cref="InvocationHandlerAttribute"/>. It provides a unified interface for
/// invoking methods with different signatures and handling both synchronous and asynchronous results.
/// </para>
/// <para>
/// Supported method signatures:
/// <list type="bullet">
/// <item><description>Methods with no parameters: <c>void Method()</c> or <c>T Method()</c></description></item>
/// <item><description>Methods with one parameter: <c>void Method(T param)</c> or <c>U Method(T param)</c></description></item>
/// <item><description>Async methods: <c>Task Method()</c>, <c>Task&lt;T&gt; Method()</c>, etc.</description></item>
/// </list>
/// </para>
/// </remarks>
public class InvocationHandlerInfo
{
    /// <summary>
    /// Gets the <see cref="MethodInfo"/> representing the method that can be invoked.
    /// </summary>
    /// <value>
    /// The method information containing details about the method signature, parameters, and return type.
    /// </value>
    public MethodInfo MethodInfo { get; }

    /// <summary>
    /// Gets the type of the parameter that the method expects, or null if the method takes no parameters.
    /// </summary>
    /// <value>
    /// The parameter type for single-parameter methods, or null for parameterless methods.
    /// This is used for deserializing incoming message bodies to the correct type.
    /// </value>
    public Type? BodyType { get; }

    /// <summary>
    /// Gets the object instance on which the method will be invoked.
    /// </summary>
    /// <value>
    /// The target object for method invocation. This is typically the MessageHandler instance
    /// that contains the decorated method.
    /// </value>
    public object Object { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvocationHandlerInfo"/> class with the specified object and method.
    /// </summary>
    /// <param name="obj">
    /// The object instance on which the method will be invoked.
    /// </param>
    /// <param name="methodInfo">
    /// The <see cref="MethodInfo"/> representing the method to be invoked.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the method has more than one parameter, which is not supported.
    /// </exception>
    /// <remarks>
    /// During construction, this class analyzes the method signature to determine the body type
    /// for parameter deserialization. Methods with zero parameters will have a null body type,
    /// while methods with one parameter will have the parameter type as the body type.
    /// </remarks>
    public InvocationHandlerInfo(object obj, MethodInfo methodInfo)
    {
        Object = obj;
        MethodInfo = methodInfo;

        var @params = MethodInfo.GetParameters();
        if (@params.Length == 0)
        {
            BodyType = null;
        }
        else if (@params.Length == 1)
        {
            BodyType = @params[0].ParameterType;
        }
        else
        {
            throw new InvalidOperationException("Method handler has more than one parameter");
        }
    }

    /// <summary>
    /// Invokes the method with the specified body parameter and returns the result.
    /// </summary>
    /// <param name="body">
    /// The parameter value to pass to the method, or null for parameterless methods.
    /// This should match the type specified by <see cref="BodyType"/>.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous method invocation. The task result contains
    /// the return value of the method, or null for void methods. For async methods,
    /// this automatically awaits the result.
    /// </returns>
    /// <remarks>
    /// This method handles both synchronous and asynchronous method invocations automatically.
    /// For methods that return Task or Task&lt;T&gt;, the result will be awaited before returning.
    /// For void methods, the result will be null.
    /// </remarks>
    /// <exception cref="TargetException">
    /// Thrown when the method invocation fails due to invalid target or parameters.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the body parameter doesn't match the expected type.
    /// </exception>
    public virtual async Task<object?> Invoke(object? body)
    {
        object?[] args = BodyType != null ? [body] : [];
        var result = MethodInfo.Invoke(Object, args);
        return await result.AwaitIfAwaitable();
    }
}
