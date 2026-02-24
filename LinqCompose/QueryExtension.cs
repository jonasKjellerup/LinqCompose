using System.Linq.Expressions;

namespace LinqTools;

public static class QueryExtension
{
    public static IQueryable<T> IncludeDependenciesOf<T, TResult>(
        this IQueryable<T> query,
        params Expression<Func<T, TResult>>[] expressions)
    {
        var dependencies = new HashSet<PropertyDependency>();
        foreach (var p in expressions.SelectMany(ProjectionTracker.FindPropertyDependencies))
        {
            dependencies.Add(dependencies.TryGetValue(p, out var dep) ? dep.Union(p) : p);
        }
        return query.Select(ProjectionBuilder.MakeProjection<T>(dependencies));
    }
}