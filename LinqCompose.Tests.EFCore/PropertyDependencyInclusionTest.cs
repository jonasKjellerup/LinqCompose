using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace LinqTools.Tests.EFCore;

public class Tests
{

    private TestDbContext _context;

    public Tests()
    {
        using var context = TestDbContext.Create();
        if (context.PropertyDependencyTestModelA.Any() || context.PropertyDependencyTestModelB.Any())
        {
            context.PropertyDependencyTestModelB.ExecuteDelete();
            context.PropertyDependencyTestModelA.ExecuteDelete(); 
        }
        
        for (var i = 1; i <= 20; i++)
        {
            var a = new PropertyDependencyTestModelA
            {
                Id = Guid.NewGuid(),
                Control = true,
            };
            for (var j = 1; j <= 10; j++)
            {
                a.Bs.Add(new PropertyDependencyTestModelB
                {
                    Id = Guid.NewGuid(),
                    Value = i*j,
                    Control = true,
                });
            }
            context.Add(a);
        }

        context.SaveChanges();
    }
    
    [SetUp]
    public void Setup()
    {
        _context = TestDbContext.Create();
    }

    [Test]
    public void IncludeDependenciesOf_Simple()
    {
        Expression<Func<PropertyDependencyTestModelA, object>> mapping = a => new { a.Id, Bs = a.Bs.Select(b => b.Value) };
        var a = _context.PropertyDependencyTestModelA
            .AsQueryable()
            .IncludeDependenciesOf(mapping)
            .ToList();
        var bs = a.SelectMany(x => x.Bs).ToList();
        
        Assert.Multiple(() =>
        {
            Assert.That(a, Has.Count.EqualTo(20));
            Assert.That(bs, Has.Count.EqualTo(20 * 10));
            Assert.That(a.All(x => x.Control is false && x.Id != Guid.Empty));
            Assert.That(bs.All(x => x is { Control: false, Value: > 0 }));
        });
    }

    [Test]
    public void IncludeDependenciesOf_Simple_Multiple()
    {
        Expression<Func<PropertyDependencyTestModelA, object>> mapping = a => new { Bs = a.Bs.Select(b => b.Value) };
        var a = _context.PropertyDependencyTestModelA
            .AsQueryable()
            .IncludeDependenciesOf(mapping, a => a.Id)
            .ToList();
        var bs = a.SelectMany(x => x.Bs).ToList();
        
        Assert.Multiple(() =>
        {
            Assert.That(a, Has.Count.EqualTo(20));
            Assert.That(bs, Has.Count.EqualTo(20 * 10));
            Assert.That(a.All(x => x.Control is false && x.Id != Guid.Empty));
            Assert.That(bs.All(x => x is { Control: false, Value: > 0 }));
        });
    }
    
    [Test]
    public void Test2()
    {
        Expression<Func<int, bool>> r = x => x > 10;
        _ = _context.PropertyDependencyTestModelB
            .Where(ExpressionContext.With(r)
                .SubstituteIn<PropertyDependencyTestModelB, bool>(r => b => r(b.Value)))
            .First();
        
        Assert.Pass();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }
}

public class PropertyDependencyTestModelA
{
    public Guid Id { get; set; }
    public bool Control { get; set; }
    public List<PropertyDependencyTestModelB> Bs { get; set; } = [];
}

public class PropertyDependencyTestModelB
{
    public Guid Id { get; set; }
    public int Value { get; set; }
    public bool Control { get; set; }
}