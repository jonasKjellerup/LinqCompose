using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Reflection;

namespace LinqTools;

/// <summary>
/// Represents a tree of property dependencies.
/// Equality is defined exclusively in terms of the root property.
/// E.g.: (a, [b, c]) == (a, []), but (a, []) != (b, []).
/// This is done to simplify HashSet lookups.
///
/// The tree (a, [(b, [c, d]), e]) corresponds to the expression:
/// x => new { a = new { b = new { x.a.b.c, x.a.b.d }, e = x.a.e }}
/// </summary>
public struct PropertyDependency : IEquatable<PropertyDependency>
{
    public required PropertyInfo Property;
    public HashSet<PropertyDependency>? Dependencies;
    
    /// <returns>
    /// A new dependency tree representing the union of two trees.
    /// Note of caution, dependency sets may be reused on non-overlapping dependencies.
    /// </returns>
    [Pure]
    public PropertyDependency Union(PropertyDependency other)
    {
        Debug.Assert(Property == other.Property);
        Debug.Assert(Dependencies != null && other.Dependencies != null);
        var newDependencies = other.Dependencies.Except(Dependencies).ToHashSet();
        foreach (var dependency in Dependencies)
        {
            if (other.Dependencies.TryGetValue(dependency, out var otherDependency) is false)
            {
                // There is no overlap, node can be added as is.
                newDependencies.Add(dependency);
                continue;
            }
            
            newDependencies.Add(dependency.Union(otherDependency));
        }
        
        return this with { Dependencies = newDependencies };
    }
    
    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is PropertyDependency other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Property.GetHashCode();
    }

    public bool Equals(PropertyDependency other)
    {
        return Property == other.Property;
    }

    public static bool operator ==(PropertyDependency left, PropertyDependency right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PropertyDependency left, PropertyDependency right)
    {
        return !(left == right);
    }
}