using System.Linq.Expressions;
using LinqCompose.Visitors;

namespace LinqCompose;

public static class Composer
{
    private static readonly ComposeVisitor ComposeVisitor = new();
    
    public static Expression<T> Compose<T>(Expression<T> expression) where T : Delegate
    {
        return (Expression<T>)ComposeVisitor.Visit(expression);
    }
    
    public static Expression<Func<T1, T2>> Compose<T1, T2>(Expression<Func<T1, T2>> expression)
    {
        return (Expression<Func<T1, T2>>)ComposeVisitor.Visit(expression);
    }
    
    public static Expression<Func<T1, T2, T3>> Compose<T1, T2, T3>(Expression<Func<T1, T2, T3>> expression)
    {
        return (Expression<Func<T1, T2, T3>>)ComposeVisitor.Visit(expression);
    }
    
    public static Expression<Func<T1, T2, T3, T4>> Compose<T1, T2, T3, T4>(Expression<Func<T1, T2, T3, T4>> expression)
    {
        return (Expression<Func<T1, T2, T3, T4>>)ComposeVisitor.Visit(expression);
    }

    public static T Inline<T>(Expression expression)
    {
        throw new InvalidOperationException($"{nameof(Inline)} can only be called inside an expression passed to {nameof(Compose)}.");
    }
    
    public static T Inline<T>(Expression<T> expression)
    {
        throw new InvalidOperationException($"{nameof(Inline)} can only be called inside an expression passed to {nameof(Compose)}.");
    }
    
}