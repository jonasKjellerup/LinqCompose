using System.Diagnostics;
using System.Linq.Expressions;

namespace LinqCompose.Visitors;

/*
 * Given target: x and replacement: (a, b) => a + b
 * Expression: x => x(y, z), reduces to: y + z
 */
internal class ReductionVisitor(ParameterExpression target, Expression replacement) : ExpressionVisitor
{
    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        if (node.Parameters.All(n => n != target))
        {
            return base.VisitLambda(node);
        }
        
        if (node.Parameters.Count == 1)
        {
            return Visit(node.Body);
        }
        
        return Expression.Lambda(
            Visit(node.Body),
            node.Name,
            node.TailCall,
            node.Parameters.Where(n => n != target));
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        if (replacement is not LambdaExpression replacementLambda 
            || node.Expression is not ParameterExpression parameter || parameter != target)
        {
            return base.VisitInvocation(node);
        }

        Debug.Assert(replacementLambda.Parameters.Count == node.Arguments.Count);
        return replacementLambda.Parameters
            .Zip(node.Arguments)
            .Select(pair => new ReductionVisitor(pair.First, pair.Second))
            .Aggregate(replacementLambda.Body, (expression, visitor) => visitor.Visit(expression));

    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        return node == target 
            ? replacement 
            : base.VisitParameter(node);
    }
}