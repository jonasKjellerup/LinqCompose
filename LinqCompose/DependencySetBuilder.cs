using System.Diagnostics.Contracts;
using System.Reflection;

namespace LinqTools;

public class DependencySetBuilder
{
    private readonly HashSet<PropertyDependency> _dependencies = [];

    public DependencySetBuilder AddProperty(PropertyInfo propertyInfo)
    {
        var dep = new PropertyDependency
        {
            Property = propertyInfo,
            Dependencies = [],
        };

        if (_dependencies.TryGetValue(dep, out var value))
        {
            _dependencies.Add(value.Union(dep));
        }
        else
        {
            _dependencies.Add(dep);
        }

        return this;
    }
    
    [Pure]
    public HashSet<PropertyDependency> Result()
    {
        var set = new HashSet<PropertyDependency>(_dependencies);
        return set;
    }
}