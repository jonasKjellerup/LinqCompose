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

    public static HashSet<PropertyDependency> FindPropertyDependencies(LambdaExpression expression)
    {
        return new ProjectionTracker().GetProjectedProperties(expression);
    }
    
    public HashSet<PropertyDependency> GetProjectedProperties(LambdaExpression expression)
    {
        Debug.Assert(expression.Parameters.Count == 1);
        return GetProjectedProperties(expression, expression.Parameters[0]);
    }
    
    private HashSet<PropertyDependency> GetProjectedProperties(LambdaExpression expression, ParameterExpression entityParameter)
    {
        Debug.Assert(expression.Parameters.Contains(entityParameter));
        _properties = [];
        _entityParameter = entityParameter;
        
        _ = Visit(expression);
        return _properties;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        var dependencies = FindDependencies(node);
        return dependencies is null ? base.VisitMember(node) : node;
    }

    /*
     * Looks for invocations of IEnumerable/Linq calls so we can detect dependencies
     * in relevant lambda arguments.
     */
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var methodParams = node.Method.GetParameters();
        var isEnumerableMethod = node.Method.IsStatic && node.Method.DeclaringType == typeof(Enumerable);
        // Methods are only relevant if they have at least two parameters since we are looking for
        // an IEnumerable<T> `this` parameter along with at least one Function where T is in parameter list.
        if (isEnumerableMethod is false || methodParams.Length < 2)
        {
            return base.VisitMethodCall(node);
        }

        var targetEnumerable = node.Arguments[0];
        // It is assumed that all methods on the Enumerable type are extensions on IEnumerable<>
        Debug.Assert(targetEnumerable.Type.IsConstructedGenericType && targetEnumerable.Type
                         .IsAssignableTo(typeof(IEnumerable<>).MakeGenericType(targetEnumerable.Type.GenericTypeArguments[0])));
        
        // Method is only relevant if the IEnumerable argument is part of the dependency tree
        var dependencies = FindExtensionMethodDependency(node);
        if (dependencies is null)
        {
            return base.VisitMethodCall(node);
        }
        
        var elementType = targetEnumerable.Type.GetGenericArguments()[0];
        // Here "Aliasing" means that it takes a lambda with a parameter that represents the enumerable element.
        var aliasedDependencies = node.Arguments
            .Where(arg => arg is LambdaExpression l && l.Parameters.Any(p => p.Type == elementType))
            .Select(arg =>
            {
                var l = (LambdaExpression)arg;
                return l.Parameters
                    .Where(p => p.Type == elementType)
                    .Select(p => dependencies.Value with
                    {
                        Dependencies = new ProjectionTracker().GetProjectedProperties(l, p)
                    })
                    .Aggregate((a, b) => a.Union(b));
            })
            .Aggregate((a, b) => a.Union(b));

        var dependencyUnion = dependencies.Value.Union(aliasedDependencies);
        
        // TODO Do this in a less ridiculous manner.
        Debug.Assert(dependencyUnion.Dependencies != null);
        var set = dependencies.Value.Dependencies;
        set!.Clear();
        foreach (var d in dependencyUnion.Dependencies)
        {
            set.Add(d);
        }
        
        return base.VisitMethodCall(node);
    }

    private PropertyDependency? FindExtensionMethodDependency(MethodCallExpression node)
    {
        while (true)
        {
            Debug.Assert(node.Arguments.Count >= 1);
            var expr = node.Arguments[0];
            switch (expr)
            {
                case MethodCallExpression { Arguments.Count: 0 }:
                    break;
                case MethodCallExpression callNode:
                    node = callNode;
                    continue;
                case MemberExpression memberNode:
                    return FindDependencies(memberNode);
            }

            return null;
        }
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