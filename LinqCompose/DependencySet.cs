using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq.Expressions;
using System.Reflection;
using LinqCompose.Visitors;

namespace LinqCompose;

public class DependencySet(Type rootType, bool validatePath = false)
{
    private readonly HashSet<PropertyDependency> _dependencies = [];
    
    public DependencySet Add(PropertyInfo propertyInfo)
    {
        return AddPath(propertyInfo);
    }
    
    /// <summary>
    /// Adds a sequence of nested dependencies.
    /// For this to generate a valid dependency tree, each specified property
    /// must be a member of the previously given property.
    /// If <see cref="validatePath"/> is set to `true` then failing to uphold this will throw an error.
    /// </summary>
    public DependencySet AddPath(params PropertyInfo[] properties)
    {
        if (properties.Length == 0)
        {
            return this;
        }

        var targetType = rootType;

        var deps = _dependencies;
        
        foreach (var property in properties)
        {
            if (validatePath)
            {
                ValidateDependency(targetType, property);
            }
            
            Debug.Assert(deps is not null);
            if (deps.TryGetValue(new PropertyDependency { Property = property }, out var node))
            {
                deps = node.Dependencies;
            }
            else
            {
                var prop = new PropertyDependency { Property = property, Dependencies = [] };
                deps.Add(prop);
                deps = prop.Dependencies;
            }
            
            targetType = property.PropertyType;

        }
        
        return this;
    }

    public DependencySet Add<T, Tr>(Expression<Func<T, Tr>> expr)
    {
        if (typeof(T) != rootType)
        {
            throw new InvalidOperationException($"The parameter `{expr.Parameters[0].Name}` must be of type {rootType.Name}.");
        }
        
        var tracker = new ProjectionTracker();
        var deps = tracker.GetProjectedProperties(expr);
        foreach (var dependency in deps)
        {
            if (_dependencies.TryGetValue(dependency, out var existing))
            {
                _dependencies.Remove(dependency);
                _dependencies.Add(existing.Union(dependency));
            }
            else
            {
                _dependencies.Add(dependency);
            }
        }

        return this;
    }

    private static void ValidateDependency(Type target, PropertyInfo property)
    {
        if (property.DeclaringType?.IsAssignableFrom(target) is not true)
        {
            throw new InvalidOperationException($"Property {property.Name} must exist on type {target.Name}");
        }
    }
    
    /// <summary>
    /// Get a reference to the dependency tree.
    /// </summary>
    [Pure]
    public HashSet<PropertyDependency> ToHashSet()
    {
        return _dependencies.ToHashSet();
    }
}

public class DependencySet<T>(bool validatePath = true) : DependencySet(typeof(T), validatePath)
{
    private readonly HashSet<PropertyDependency> _dependencies = [];

    public DependencySet<T> Add<Tr>(Expression<Func<T, Tr>> expr)
    {
        var tracker = new ProjectionTracker();
        var deps = tracker.GetProjectedProperties(expr);
        foreach (var dependency in deps)
        {
            if (_dependencies.TryGetValue(dependency, out var existing))
            {
                _dependencies.Remove(dependency);
                _dependencies.Add(existing.Union(dependency));
            }
            else
            {
                _dependencies.Add(dependency);
            }
        }

        return this;
    }
}