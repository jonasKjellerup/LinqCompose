using System.Linq.Expressions;

namespace LinqTools;

public static class QueryExtension
{
    public static IQueryable<T> IncludeDependenciesOf<T, TResult>(
        this IQueryable<T> query,
        Expression<Func<T, TResult>> expression)
    {
        var dependencies = ProjectionTracker.FindPropertyDependencies(expression);
        return query.Select(ProjectionBuilder.MakeProjection<T>(dependencies));
    }
}