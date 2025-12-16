using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace LinqTools;

/**
 * Tracks which entity properties should be included in a projection,
 * based on usage within a lambda function.
 */
internal class ProjectionTracker : ExpressionVisitor
{
    private HashSet<PropertyInfo> _properties = [];
    private ParameterExpression? _entityParameter = null;

    public HashSet<PropertyInfo> GetProjectedProperties(LambdaExpression expression)
    {
        Debug.Assert(expression.Parameters.Count == 1);
        _properties = [];
        _entityParameter = expression.Parameters[0];
        
        _ = Visit(expression);
        return _properties;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        var root = FindMemberExpressionRoot(node.Expression);
        if (root != _entityParameter)
        {
            return base.VisitMember(node);
        }
        
        if (node.Member is PropertyInfo propertyInfo)
        {
            _properties.Add(propertyInfo);
        }
        
        return node;
    }

    private Expression? FindMemberExpressionRoot(Expression? node)
    {
        while (node is not null)
        {
            if (node == _entityParameter)
            {
                return node;
            }
        
            if (node is MemberExpression { Expression: not null } member)
            {
                node = member.Expression;
            }
            else
            {
                node = null;
            }
        }

        return null;
    }
}