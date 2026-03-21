using System.Diagnostics.Contracts;
using System.Linq.Expressions;

namespace LinqCompose;

public static class ExpressionContext
{
    [Pure]
    [Obsolete("Use Composer.Compose and Composer.Inline instead.")]
    public static ExpressionContext<T> With<T>(Expression<T> expression)
    {
        return new ExpressionContext<T>(expression);
    }
}

public class ExpressionContext<T>(Expression<T> contextExpression)
{
    [Pure]
    [Obsolete("Use Composer.Compose and Composer.Inline instead.")]
    public Expression<Func<TIn, TOut>> SubstituteIn<TIn, TOut>(Expression<Func<T, Expression<Func<TIn, TOut>>>> expression)
    {
        var reducer = new ReductionVisitor(expression.Parameters[0], contextExpression);
        var result = (UnaryExpression)reducer.Visit(expression.Body);
        return (Expression<Func<TIn, TOut>>)result.Operand;
    }
}

public static class QueryableExtensions
{
    [Pure]
    [Obsolete("Use Composer.Compose and Composer.Inline instead.")]
    public static IQueryable<T> SWhere<T, TE>(this IQueryable<T> query, Expression<TE> expression,
        Expression<Func<TE, Func<T, bool>>> predicate
    )
    {
        var reducer = new ReductionVisitor(predicate.Parameters[0], expression);
        return query.Where((Expression<Func<T, bool>>)((UnaryExpression)reducer.Visit(expression.Body)).Operand);
    }

    [Pure]
    [Obsolete("Use Composer.Compose and Composer.Inline instead.")]
    public static IQueryable<TR> SSelect<T, TR, TE>(this IQueryable<T> query, Expression<TE> expression,
        Expression<Func<TE, Func<T, TR>>> selector
    )
    {
        var reducer = new ReductionVisitor(selector.Parameters[0], expression);
        return query.Select((Expression<Func<T, TR>>)((UnaryExpression)reducer.Visit(expression.Body)).Operand);
    }
}
