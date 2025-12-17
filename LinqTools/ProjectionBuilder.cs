using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace LinqTools;

public static class ProjectionBuilder
{
    private static readonly Type EnumerableType = typeof(Enumerable);
    private static readonly MethodInfo SelectMethod = EnumerableType.GetMethod(nameof(Enumerable.Select), BindingFlags.Public | BindingFlags.Static)!;
    private static readonly MethodInfo ToArrayMethod = EnumerableType.GetMethod(nameof(Enumerable.ToArray), BindingFlags.Public | BindingFlags.Static)!;
    private static readonly MethodInfo ToListMethod = EnumerableType.GetMethod(nameof(Enumerable.ToList), BindingFlags.Public | BindingFlags.Static)!;
    private static readonly MethodInfo ToHashSetMethod = EnumerableType.GetMethod(nameof(Enumerable.ToHashSet), BindingFlags.Public | BindingFlags.Static)!;
    
    public static IQueryable<T> ApplyProjectionMapping<T, TMapping>(this IQueryable<T> queryable, Expression<Func<T, TMapping>> mapping)
    {
        // TODO memoize
        var tracker = new ProjectionTracker();
        var props = tracker.GetProjectedProperties(mapping);
        var projection = MakeProjection<T>(props);

        return queryable.Select(projection);
    }
    
    /// <summary>
    /// Creates a lambda expression that projects the selected properties on the type itself.
    /// E.g.: consider a type T with properties A, B, and C, making a projection of properties A and B
    /// will result in the following expression:
    /// <code>
    /// p => new T { A = p.A, B = p.B }
    /// </code>
    ///
    /// This is used to extract an expression from a mapping function that needs to run in-memory,
    /// such that Entity Framework can retrieve only required fields from database.
    /// </summary>
    /// <param name="properties">The properties to include in the projection.</param>
    /// <typeparam name="T">The target entity of your IQueryable. Must be default constructible.</typeparam>
    /// <returns>A lambda expression that projects the specified fields.</returns>
    public static Expression<Func<T, T>> MakeProjection<T>(HashSet<PropertyDependency> properties)
    {
        var param = Expression.Parameter(typeof(T));
        var initExpression = MakeObjectInitializer(typeof(T), param, properties);
        
        return Expression.Lambda<Func<T, T>>(initExpression, param);
    }

    private static MemberInitExpression MakeObjectInitializer(
        Type type, 
        Expression memberSource,
        HashSet<PropertyDependency> properties)
    {
        Debug.Assert(properties.All(p => p.Property.ReflectedType == type), 
            $"All of the specified properties must exist on {type.Name}");
        var bindings = properties.Select(p =>
        {
            var access = Expression.MakeMemberAccess(memberSource, p.Property);
            Debug.Assert(p.Dependencies is not null);
            var isEnumerable = typeof(IEnumerable).IsAssignableFrom(access.Type);
            return p.Dependencies.Count switch
            {
                > 0 when isEnumerable => Expression.Bind(p.Property, MakeEnumerableProjection(access, p)),
                > 0 => Expression.Bind(p.Property, MakeObjectInitializer(p.Property.PropertyType, access, p.Dependencies)),
                _ => Expression.Bind(p.Property, access)
            };
        });

        return Expression.MemberInit(Expression.New(type), bindings);
    }

    private static MethodCallExpression MakeEnumerableProjection(
        Expression memberSource,
        PropertyDependency property)
    {
        Debug.Assert(property.Property.ReflectedType == memberSource.Type);
        Debug.Assert(property.Dependencies is { Count: > 0 });
        var member = Expression.Property(memberSource, property.Property);
        
        var elementType = GetEnumerableElementType(member.Type);
        var lambdaParam = Expression.Parameter(elementType);
        var initializer = MakeObjectInitializer(elementType, lambdaParam, property.Dependencies);
        var lambda = Expression.Lambda(initializer, lambdaParam);

        var genericSelect = SelectMethod.MakeGenericMethod(elementType, elementType);
        var collectionType = GetCollectionType(member.Type);
        return MakeCollectEnumerableExpression(Expression.Call(genericSelect, member, lambda), collectionType);
    }

    private static SupportedCollectionType GetCollectionType(Type type)
    {
        if (type.IsArray)
        {
            return SupportedCollectionType.Array;
        }
        
        if (type.IsGenericType is false)
        {
            throw new InvalidOperationException($"{type.FullName} is not valid for enumerable projection.");
        }

        var genericType = type.GetGenericTypeDefinition();
        if (genericType == typeof(IEnumerable<>))
        {
            return SupportedCollectionType.AbstractEnumerable;
        }

        if (genericType == typeof(List<>))
        {
            return SupportedCollectionType.List;
        }

        if (genericType == typeof(HashSet<>))
        {
            return SupportedCollectionType.HashSet;
        }
        
        throw new InvalidOperationException($"{type.FullName} is not valid for enumerable projection.");
    }

    private static Type GetEnumerableElementType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType()!;
        }
        
        Debug.Assert(type.IsGenericType);
        return type.GetGenericArguments()[0];
    }

    private static MethodCallExpression MakeCollectEnumerableExpression(MethodCallExpression source, SupportedCollectionType type)
    {
        return type switch
        {
            SupportedCollectionType.Array => Expression.Call(ToArrayMethod, source),
            SupportedCollectionType.AbstractEnumerable => source,
            SupportedCollectionType.List => Expression.Call(ToListMethod, source),
            SupportedCollectionType.HashSet => Expression.Call(ToHashSetMethod, source),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
    
    private enum SupportedCollectionType
    {
        Array, // T[]
        AbstractEnumerable, // IEnumerable<T>
        List, // List<T>
        HashSet, // HashSet<T>
    }
    
}