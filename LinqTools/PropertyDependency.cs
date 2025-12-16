using System.Diagnostics.CodeAnalysis;
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

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is PropertyDependency other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Property, Dependencies);
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