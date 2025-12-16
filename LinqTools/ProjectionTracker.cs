using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LinqTools;

/**
 * Tracks which entity properties should be included in a projection,
 * based on usage within a lambda function.
 */
internal class ProjectionTracker : ExpressionVisitor
{
    private HashSet<PropertyDependency> _properties = [];
    private ParameterExpression? _entityParameter = null;

    public HashSet<PropertyDependency> GetProjectedProperties(LambdaExpression expression)
    {
        Debug.Assert(expression.Parameters.Count == 1);
        _properties = [];
        _entityParameter = expression.Parameters[0];
        
        _ = Visit(expression);
        return _properties;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        var dependencies = FindDependencies(node);
        return dependencies is null ? base.VisitMember(node) : node;
    }

    private PropertyDependency? FindDependencies(Expression? node)
    {
        if (node is not MemberExpression member)
        {
            return null;
        }

        // All members must be properties to be considered
        if (member.Member is not PropertyInfo propertyInfo)
        {
            return null;
        }
        
        var d = new PropertyDependency
        {
            Property = propertyInfo,
            Dependencies = null,
        };

        HashSet<PropertyDependency> knownDependencies;
        if (member.Expression == _entityParameter)
        {
            knownDependencies = _properties;
        }
        else
        {
            var result = FindDependencies(member.Expression);
            if (result is null)
            {
                return null;
            }

            Debug.Assert(result.Value.Dependencies is not null);
            knownDependencies = result.Value.Dependencies;
        }

        if (knownDependencies.TryGetValue(d, out var existing))
        {
            return existing;
        }
        
        d.Dependencies = [];
        knownDependencies.Add(d);
        return d;
    }
}