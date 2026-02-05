using System.Diagnostics.Contracts;
using System.Linq.Expressions;

namespace LinqTools;

public static class ExpressionContext
{
    public static ExpressionContext<T> With<T>(Expression<T> expression)
    {
        return new ExpressionContext<T>(expression);
    }
}

public class ExpressionContext<T>(Expression<T> contextExpression)
{
    public Expression<Func<TIn, TOut>> SubstituteIn<TIn, TOut>(Expression<Func<T, Expression<Func<TIn, TOut>>>> expression)
    {
        var reducer = new ReductionVisitor(expression.Parameters[0], contextExpression);
        var result = (UnaryExpression)reducer.Visit(expression.Body);
        return (Expression<Func<TIn, TOut>>)result.Operand;
    }
}

public static class QueryableExtensions
{
    extension<T>(IQueryable<T> query)
    {
        [Pure]
        public IQueryable<T> SWhere<TE>(Expression<TE> expression,
            Expression<Func<TE, Func<T, bool>>> predicate
        )
        {
            var reducer = new ReductionVisitor(predicate.Parameters[0], expression);
            return query.Where((Expression<Func<T, bool>>)((UnaryExpression)reducer.Visit(expression.Body)).Operand);
        }

        [Pure]
        public IQueryable<TR> SSelect<TR, TE>(Expression<TE> expression,
            Expression<Func<TE, Func<T, TR>>> selector
        )
        {
            var reducer = new ReductionVisitor(selector.Parameters[0], expression);
            return query.Select((Expression<Func<T, TR>>)((UnaryExpression)reducer.Visit(expression.Body)).Operand);
        }
    }
}
