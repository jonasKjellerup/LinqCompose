using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Reflection;

namespace LinqCompose;

public class DependencySetBuilder(bool validatePath = false)
{
    private readonly HashSet<PropertyDependency> _dependencies = [];

    public DependencySetBuilder AddProperty(PropertyInfo propertyInfo)
    {
        return AddPropertyPath(propertyInfo);
    }
    
    /// <summary>
    /// Adds a sequence of nested dependencies.
    /// For this to generate a valid dependency tree, each specified property
    /// must be a member of the previously given property.
    /// If <see cref="validatePath"/> is set to `true` then failing to uphold this will throw an error.
    /// </summary>
    public DependencySetBuilder AddPropertyPath(params PropertyInfo[] properties)
    {
        if (properties.Length == 0)
        {
            return this;
        }

        Type? targetType = null;

        var deps = _dependencies;
        
        foreach (var property in properties)
        {
            if (validatePath && targetType is not null)
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
    public HashSet<PropertyDependency> Result()
    {
        return _dependencies;
    }
}