using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace LinqTools;

public static class ProjectionBuilder
{
    /// <summary>
    /// Creates a lambda expression that projects the selected properties on the type itself.
    /// E.g. consider a type T with properties A, B, and C, making a projection of properties A and B
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
    public static Expression<Func<T, T>> MakeProjection<T>(IEnumerable<PropertyInfo> properties)
    {
        var type = typeof(T);
        // TODO handle navigational properties.
        var props = properties.DistinctBy(p => p.Name).ToHashSet();
        Debug.Assert(props.All(p => p.ReflectedType == type), 
            $"All of the specified properties must exist on {type.Name}");
        
        var param = Expression.Parameter(typeof(T));
        var bindings = props.Select(p =>
        {
            var access = Expression.MakeMemberAccess(param, p);
            return Expression.Bind(p, access);
        });
        var initExpression = Expression.MemberInit(Expression.New(typeof(T)), bindings);
        
        return Expression.Lambda<Func<T, T>>(initExpression, param);
    }

    public static IQueryable<T> ApplyProjectionMapping<T, TMapping>(this IQueryable<T> queryable, Expression<Func<T, TMapping>> mapping)
    where T : new()
    {
        // TODO memoize
        var tracker = new ProjectionTracker();
        var props = tracker.GetProjectedProperties(mapping);
        var projection = MakeProjection<T>(props);

        return queryable.Select(projection);
    }
}