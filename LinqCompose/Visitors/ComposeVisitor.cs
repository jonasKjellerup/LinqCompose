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
        // We explicitly use the base.Visit to ensure that the Inline target isn't automatically unwrapped.
        // as this would prevent us from reducing it here.
        var r = base.Visit(node.Expression);
        if (r is ConstantExpression { Value: Inline(var inlineTarget) })
        {
            return node.Arguments
                .Zip(inlineTarget.Parameters)
                .Aggregate(
                    (Expression)inlineTarget,
                    (lambda, pair) => new ReductionVisitor(pair.Second, pair.First).Visit(lambda)
                );
        }

        return base.VisitInvocation(node);
    }
    
    
    
    private static LambdaExpression UnwrapLambdaArgument(Expression expression)
    {
        // Inline lambda expressions are simply just a Unary expression that can be unwrapped
        // If it has been explicitly cast to an expression, then it will be wrapped in multiple,
        // unary expression, hence the use of ReduceUnary.
        if (expression is UnaryExpression unary)
        {
            return (LambdaExpression)ReduceUnary(unary);
        }

        /*
         * References to anything outside the lambda will be a field access on an unnameable closure object.
         */
        return ReadClosureExpression<LambdaExpression>(expression);
    }

    private static Expression ReduceUnary(UnaryExpression unary)
    {
        do
        {
            var inner = unary.Operand;
            if (inner is not UnaryExpression un)
            {
                return inner;
            }

            unary = un;
        } while (true);
    }

    private static T ReadClosureExpression<T>(Expression expression)
    {
        /*
         * The syntax node is expected to look like this: (closureObj).localVarName
         * The closure is passed as a constant, so we can read fields on it and extract the lambda expression.
         */
        Debug.Assert(expression is MemberExpression);
        var memberExpression = (MemberExpression)expression;

        Debug.Assert(memberExpression.Expression is ConstantExpression);
        var constant = (ConstantExpression)memberExpression.Expression;

        Debug.Assert(memberExpression.Member is FieldInfo);
        return (T)((FieldInfo)memberExpression.Member).GetValue(constant.Value)!;
    }

    private record Inline(LambdaExpression Expr);
}