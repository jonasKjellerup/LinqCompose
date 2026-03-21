using System.Reflection;

namespace LinqCompose.Utility;

internal static class MethodInfoHelper
{
    /// <summary><![CDATA[
    /// Retrieves the MethodInfo representing a concrete method.
    /// Given `Ennumerable.ToArray<object>` this function will return the MethodInfo
    /// corresponding to `Enumarable.ToArray<object>`. Use GetGenericMethod if you want the MethodInfo
    /// for `Ennumerable.ToArray<>`.
    ///
    /// For parameter overloaded methods, explicitly stating the type of TFunc is an effective
    /// way to distinguish between overloads.
    /// ]]></summary>
    public static MethodInfo GetMethod<TFunc>(TFunc func) where TFunc : Delegate
    {
        return func.Method;
    }
    
    /// <summary><![CDATA[
    /// Retrieves the MethodInfo representing an open generic method.
    /// Given `Ennumerable.ToArray<object>` this function will return the MethodInfo
    /// corresponding to `Enumarable.ToArray<>`. Use GetMethod if you want the MethodInfo
    /// for `Ennumerable.ToArray<object>`.
    ///
    /// For parameter overloaded methods, explicitly stating the type of TFunc is an effective
    /// way to distinguish between overloads.
    /// ]]></summary>
    public static MethodInfo GetGenericMethod<TFunc>(TFunc func) where TFunc : Delegate
    {
        return func.Method.GetGenericMethodDefinition();
    }
}