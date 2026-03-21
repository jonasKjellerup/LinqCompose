# LinqCompose

[![latest version](https://img.shields.io/nuget/v/LinqCompose)](https://www.nuget.org/packages/LinqCompose)

LinqCompose is a library that aims to simplify abstracting and composing LINQ operations,

Features:
 - Generating expressions for selecting only required properties, based on a mapping expression.
 - Substituting expression placeholders in an expression.

## Installation

```sh
dotnet add package LinqCompose
```

# Examples

## EF Core projection from a mapping

The primary use case for generating property selection expressions from a mapping, is to use with `IQueryable<T>.Select`
when querying with Entity Framework. This effectively allows Entity Framework to only fetch the database columns that
are actually used. This is generally trivial do by hand but can become tedious when trying abstract the actualy querying
out into a service.

```csharp
class QueryService(DbContext context) 
{
    public List<T> GetEntityListAs<T>(Expression<Func<Entity, T>> mapping)
    {
        var entities = context.Entities
            .Where(x => /* some condition */)
            // Fetch fields required by mapping, but also ensure Id and RelevantProperty are fetched
            .IncludeDependenciesOf(mapping, e => new { e.Id, e.RelevantProperty })
            .ToList();
        
        // Perform any necessary computations/queries using relevant fetched properties.
        
        var m = mapping.compile();
        return entities.Select(m).ToList();
    }
}

class Service(QueryService queryService) 
{
    public void DoSomething() 
    {
        var data = queryService.GetEntityListAs(e => new { e.Name, e.PropA, e.RelatedEntity.PropB });
        // Do something with the data...
    }
}
```

## Reusing expression segments in other expressions

One challenge when working with Entity Framework and LINQ expressions is reusing logic that needs to be consistent
across many queries. Entity Framework Core largely is not able to translate queries that include function calls
(that aren't sufficiently parametrized) in them.

On top of that, expressions representing functions cannot be called inside an expression.
LinqCompose aims to provide a means to achieve by means of a substitution expressions:

```csharp
using static LinqCompose.Composer;
Expression<Func<int, bool>> reusableExpression = x => x > 10;
var substitutedExpression = Compose<SomeObject, bool>(obj => Inline(reusableExpression)(obj.NumericValue));
// substituedExpression will be equivalent to the expression : b => b.NumericValue > 10
```

The `Compose` function generally requires explicit type parameters, as inference is insufficient here.
There are shorthand definitions for 1-3 parameter functions, but beyond this the full delegate type must be specified.
E.g.:
- Non-shorthand version: `Compose<Func<A,B,C,D,ReturnType>>((a,b,c,d) => ...)`.
- 2-parameter shorthand:  `Compose<A,B,ReturnType>((a, b) => ...)`
