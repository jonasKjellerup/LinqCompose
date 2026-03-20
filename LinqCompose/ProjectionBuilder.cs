using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace LinqCompose;

public static class ProjectionBuilder
{
    private static readonly NullabilityInfoContext NullabilityInfoContext = new();
    private static readonly Type EnumerableType = typeof(Enumerable);
    private static readonly MethodInfo SelectMethod;

    private static readonly MethodInfo ToArrayMethod =
        EnumerableType.GetMethod(nameof(Enumerable.ToArray), BindingFlags.Public | BindingFlags.Static)!;

    private static readonly MethodInfo ToListMethod =
        EnumerableType.GetMethod(nameof(Enumerable.ToList), BindingFlags.Public | BindingFlags.Static)!;

    private static readonly MethodInfo ToHashSetMethod;

    static ProjectionBuilder()
    {
        Func<IEnumerable<object>, Func<object, object>, IEnumerable<object>> select = Enumerable.Select;
        SelectMethod = select.Method.GetGenericMethodDefinition();
        Func<IEnumerable<object>, HashSet<object>> toHashSet = Enumerable.ToHashSet;
        ToHashSetMethod = toHashSet.Method.GetGenericMethodDefinition();
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
        Debug.Assert(properties.All(p => p.Property.DeclaringType!.IsAssignableFrom(type)),
            $"All of the specified properties must exist on {type.Name}");
        var bindings = properties.Select(p =>
        {
            var isNullable = IsPropertyNullable(p.Property);
            var access = Expression.MakeMemberAccess(memberSource, p.Property);
            Debug.Assert(p.Dependencies is not null);
            var isEnumerable = typeof(IEnumerable).IsAssignableFrom(access.Type);
            return p.Dependencies.Count switch
            {
                > 0 when isEnumerable && isNullable => Expression.Bind(p.Property, MakeNullConditional(
                    access,
                    MakeEnumerableProjection(access, p)
                )),
                > 0 when isEnumerable => Expression.Bind(p.Property, MakeEnumerableProjection(access, p)),
                > 0 when isNullable => Expression.Bind(p.Property, MakeNullConditional(
                    access,
                    MakeObjectInitializer(p.Property.PropertyType, access, p.Dependencies)
                )),
                > 0 => Expression.Bind(p.Property,
                    MakeObjectInitializer(p.Property.PropertyType, access, p.Dependencies)),
                _ => Expression.Bind(p.Property, access)
            };
        });

        return Expression.MemberInit(Expression.New(type), bindings);
    }

    private static bool IsPropertyNullable(PropertyInfo propertyInfo)
    {
        return NullabilityInfoContext.Create(propertyInfo).ReadState is
            NullabilityState.Nullable or NullabilityState.Unknown;
    }

    private static Expression MakeNullConditional(Expression source, Expression nonNullValue)
    {
        var isNull = Expression.Equal(source, Expression.Constant(null));
        return Expression.Condition(isNull, Expression.Constant(null, nonNullValue.Type), nonNullValue);
    }

    private static MethodCallExpression MakeEnumerableProjection(
        Expression memberSource,
        PropertyDependency property)
    {
        Debug.Assert(property.Property.PropertyType == memberSource.Type);
        Debug.Assert(property.Dependencies is { Count: > 0 });

        var elementType = GetEnumerableElementType(memberSource.Type);
        var lambdaParam = Expression.Parameter(elementType);
        var initializer = MakeObjectInitializer(elementType, lambdaParam, property.Dependencies);
        var lambda = Expression.Lambda(initializer, lambdaParam);

        var genericSelect = SelectMethod.MakeGenericMethod(elementType, elementType);
        var collectionType = GetCollectionType(memberSource.Type);
        return MakeCollectEnumerableExpression(Expression.Call(genericSelect, memberSource, lambda), elementType,
            collectionType);
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

    private static MethodCallExpression MakeCollectEnumerableExpression(MethodCallExpression source, Type elementType,
        SupportedCollectionType type)
    {
        var method = type switch
        {
            SupportedCollectionType.AbstractEnumerable => null,
            SupportedCollectionType.Array => ToArrayMethod,
            SupportedCollectionType.List => ToListMethod,
            SupportedCollectionType.HashSet => ToHashSetMethod,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        if (method is null)
        {
            return source;
        }

        method = method.MakeGenericMethod(elementType);

        return Expression.Call(method, source);
    }

    private enum SupportedCollectionType
    {
        Array, // T[]
        AbstractEnumerable, // IEnumerable<T>
        List, // List<T>
        HashSet, // HashSet<T>
    }
}