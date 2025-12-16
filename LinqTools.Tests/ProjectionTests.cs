using System.Linq.Expressions;
using System.Reflection;

namespace LinqTools.Tests;

public class ProjectionTests
{
    [Test]
    public void Projection_SimpleMembers()
    {
        Expression<Func<TestEntity, object>> mapping = e => new { e.Name, e.Id, e.SubEntities, Alias = e.Name };
        var tracker = new ProjectionTracker();
        var accessedProperties = tracker.GetProjectedProperties(mapping);
        var propertyNames = accessedProperties.Select(e => e.Name).ToHashSet();
        
        HashSet<string> expectedResult = [ nameof(TestEntity.Name), nameof(TestEntity.Id), nameof(TestEntity.SubEntities) ];
        Assert.That(propertyNames.SetEquals(expectedResult));
    }
    
    [Test]
    public void Projection_Method_IsIgnored()
    {
        Expression<Func<TestEntity, object>> mapping = e => new MethodTestEntity { Method = e.TestMethod, Invocation = e.TestMethod() };
        var tracker = new ProjectionTracker();
        var accessedProperties = tracker.GetProjectedProperties(mapping);
        var propertyNames = accessedProperties.Select(e => e.Name).ToHashSet();
        
        Assert.That(propertyNames, Is.Empty);
    }

    [Test]
    public void Projection_SubentityMember()
    {
        Expression<Func<TestEntity, object>> mapping = e => new { e.SingleSubEntity.Id };
        var tracker = new ProjectionTracker();
        var accessedProperties = tracker.GetProjectedProperties(mapping);
        var propertyNames = accessedProperties.Select(e => e.Name).ToHashSet();
        
        HashSet<string> expectedResult = [ nameof(TestEntity.SingleSubEntity.Id) ];
        Assert.That(propertyNames.SetEquals(expectedResult));
    }
    
    [Test]
    public void Projection_ClosureEntityMember()
    {
        var x = new { Test = 4 };
        Expression<Func<TestEntity, object>> mapping = e => new { e.Name, x.Test };
        var tracker = new ProjectionTracker();
        var accessedProperties = tracker.GetProjectedProperties(mapping);
        var propertyNames = accessedProperties.Select(e => e.Name).ToHashSet();
        
        HashSet<string> expectedResult = [ nameof(TestEntity.Name) ];
        Assert.That(propertyNames.SetEquals(expectedResult));
    }

    [Test]
    public void Projection_Build_DirectMembers()
    {
        var t = typeof(TestEntity);
        PropertyInfo[] props = [t.GetProperty(nameof(TestEntity.Id))!, t.GetProperty(nameof(TestEntity.SubEntities))!];

        ProjectionBuilder.MakeProjection<TestEntity>(props);
    }
    
    private class TestEntity
    {
        public required int Id { get; set; }
        public required string Name { get; set; }
        public required List<SubEntity> SubEntities { get; set; }
        public required SubEntity SingleSubEntity { get; set; }
        public SubEntity? NullableSubEntity { get; set; }

        public int TestMethod()
        {
            return 1;
        }
    }

    private class SubEntity
    {
        public required int Id { get; set; }
        public required int KeyEntityId { get; set; }
    }

    // Method group cannot be assigned in an anonymous object initializer, so we need a class.
    private class MethodTestEntity
    {
        public required Func<int> Method { get; set; }
        public required int Invocation { get; set; }
    }
}