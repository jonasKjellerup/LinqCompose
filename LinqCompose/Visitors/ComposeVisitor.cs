using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace LinqCompose.Visitors;

internal class ComposeVisitor : ExpressionVisitor
{
    private static readonly MethodInfo InlineExprInfo =
        GetGenericMethod<Func<Expression, object>>(Composer.Inline<object>);

    private static readonly MethodInfo InlineLambdaInfo =
        GetGenericMethod<Func<Expression<object>, object>>(Composer.Inline);

    public override Expression? Visit(Expression? node)
    {
        if (node is null)
        {
            return node;
        }

        var result = base.Visit(node);

        if (result is ConstantExpression { Value: Inline(var inlineTarget) })
        {
            return inlineTarget;
        }

        return result;
    }
    
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var compositionMethod = node.Method.IsConstructedGenericMethod 
            ? node.Method.GetGenericMethodDefinition()
            : node.Method;

        if (compositionMethod == InlineExprInfo)
        {
            var type = node.Method.GetGenericArguments()[0];
            Debug.Assert(node.Arguments[0].Type.IsAssignableTo(typeof(Expression)));
            var expressionNode = node.Arguments[0];
            
            // If the expression is constructing an expression inline - e.g. Expression.Constant(x)
            // then we wrap it in a lambda and compile it to get the resulting expression value.
            if (expressionNode is MethodCallExpression methodCall &&
                methodCall.Method.DeclaringType == typeof(Expression))
            {
                var expr = Expression.Lambda<Func<Expression>>(expressionNode, false).Compile();
                return expr();
            }
            
            var expression = ReadClosureExpression<Expression>(expressionNode);

            if (!expression.Type.IsAssignableTo(type))
            {
                throw new ArgumentException(
                    $"Type of the expression passed to {nameof(Composer.Inline)} must be assignable to the type given as generic argument. " +
                    $"The generic argument was {type.Name} and the type of the expression was {expression.Type}.",
                    expression.ToString()
                    );
            }

            return expression;
        }
        
        if (compositionMethod == InlineLambdaInfo)
        {
            var expressionToInline = UnwrapLambdaArgument(node.Arguments[0]);
            return Expression.Constant(new Inline(expressionToInline));
        }
        
        return base.VisitMethodCall(node);
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        var r = Visit(node.Expression);
        if (r is LambdaExpression inlineTarget)
        {
            var result = node.Arguments
                .Zip(inlineTarget.Parameters)
                .Aggregate(
                    (Expression)inlineTarget.Body,
                    (body, pair) => new ReductionVisitor(pair.Second, pair.First).Visit(body)
                );
            
            // We need to visit the result again in case it contains more Inlines 
            // that were inside the inlined lambda.
            return Visit(result);
        }

        return base.VisitInvocation(node);
    }
    
    
    
    private static LambdaExpression UnwrapLambdaArgument(Expression expression)
    {
        // If it's a lambda literal, it might be wrapped in a UnaryExpression (Convert or Quote)
        var current = expression;
        while (current is UnaryExpression unary)
        {
            current = unary.Operand;
        }

        if (current is LambdaExpression lambda)
        {
            return lambda;
        }

        /*
         * References to anything outside the lambda will be a field access on an unnameable closure object.
         */
        return ReadClosureExpression<LambdaExpression>(expression);
    }

    private static T ReadClosureExpression<T>(Expression expression)
    {
        // If the expression is already a ConstantExpression of the right type, just return it.
        if (expression is ConstantExpression { Value: T value })
        {
            return value;
        }

        /*
         * The syntax node is expected to look like this: (closureObj).localVarName
         * The closure is passed as a constant, so we can read fields on it and extract the lambda expression.
         */
        if (expression is MemberExpression { Expression: ConstantExpression constant, Member: FieldInfo field })
        {
            return (T)field.GetValue(constant.Value)!;
        }
        
        // Fallback: try to compile and execute the expression if it's not a simple closure access.
        var lambda = Expression.Lambda<Func<T>>(expression);
        return lambda.Compile()();
    }

    private record Inline(LambdaExpression Expr);
}