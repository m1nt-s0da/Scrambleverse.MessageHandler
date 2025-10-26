using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Scrambleverse.MessageHandler.Reflection;

public static class Awaitable
{
    public static async Task<object?> AwaitIfAwaitable(this object obj)
    {
        if (obj == null)
        {
            return null;
        }

        var type = obj.GetType();
        if (type.GetAwaitedResult(out var awaitedResultType))
        {
            GetAwaiterMethod(type, out var awaiterMethod);
            var awaiter = awaiterMethod.Invoke(obj, null) ?? throw new InvalidOperationException("Failed to get awaiter.");
            var awaiterType = awaiter!.GetType();

            GetIsCompletedProperty(awaiterType, out var isCompletedProperty);
            while(true)
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

