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
        var propertyNames = accessedProperties.Select(e => e.Property.Name).ToHashSet();
        
        HashSet<string> expectedResult = [ nameof(TestEntity.Name), nameof(TestEntity.Id), nameof(TestEntity.SubEntities) ];
        Assert.That(propertyNames.SetEquals(expectedResult));
    }
    
    [Test]
    public void Projection_Method_IsIgnored()
    {
        Expression<Func<TestEntity, object>> mapping = e => new MethodTestEntity { Method = e.TestMethod, Invocation = e.TestMethod() };
        var tracker = new ProjectionTracker();
        var accessedProperties = tracker.GetProjectedProperties(mapping);
        var propertyNames = accessedProperties.Select(e => e.Property.Name).ToHashSet();
        
        Assert.That(propertyNames, Is.Empty);
    }

    [Test]
    public void Projection_SubentityMember()
    {
        Expression<Func<TestEntity, object>> mapping = e => new { e.SingleSubEntity.Id };
        var tracker = new ProjectionTracker();
        var accessedProperties = tracker.GetProjectedProperties(mapping);
        var dependency = accessedProperties.Single();
        var subProperty = dependency.Dependencies!.Single();
        
        Assert.Multiple(() =>
        {
            Assert.That(dependency.Property.Name, Is.EqualTo(nameof(TestEntity.SingleSubEntity)));
            Assert.That(subProperty.Property.Name, Is.EqualTo(nameof(TestEntity.SingleSubEntity.Id)));
        });
    }
    
    [Test]
    public void Projection_ClosureEntityMember()
    {
        var x = new { Test = 4 };
        Expression<Func<TestEntity, object>> mapping = e => new { e.Name, x.Test };
        var tracker = new ProjectionTracker();
        var accessedProperties = tracker.GetProjectedProperties(mapping);
        var propertyNames = accessedProperties.Select(e => e.Property.Name).ToHashSet();
        
        HashSet<string> expectedResult = [ nameof(TestEntity.Name) ];
        Assert.That(propertyNames.SetEquals(expectedResult));
    }

    [Test]
    public void Projection_Build_DirectMembers()
    {
        var t = typeof(TestEntity);
        HashSet<PropertyDependency> props = [
            new ()
            {
                Property = t.GetProperty(nameof(TestEntity.Id))!,
                Dependencies = [],
            },
            new ()
            {
            Property = t.GetProperty(nameof(TestEntity.SubEntities))!,
            Dependencies = [],
            }
        ];

        var projectionExpression = ProjectionBuilder.MakeProjection<TestEntity>(props);
        Assert.That(
            projectionExpression.ToString(), 
            Is.EqualTo("Param_0 => new TestEntity() {Id = Param_0.Id, SubEntities = Param_0.SubEntities}")
            );
    }

    [Test]
    public void Projection_Build_SubEntities()
    {
        var t = typeof(TestEntity);
        var t2 = typeof(SubEntity);
        HashSet<PropertyDependency> props = [
            new ()
            {
                Property = t.GetProperty(nameof(TestEntity.Id))!,
                Dependencies = [],
            },
            new ()
            {
                Property = t.GetProperty(nameof(TestEntity.SingleSubEntity))!,
                Dependencies = [
                    new ()
                    {
                        Property = t2.GetProperty(nameof(TestEntity.SingleSubEntity.Id))!,
                        Dependencies = [],
                    },
                    new ()
                    {
                        Property = t2.GetProperty(nameof(TestEntity.SingleSubEntity.KeyEntityId))!,
                        Dependencies = [],
                    }
                ],
            }
        ];

        var projectionExpression = ProjectionBuilder.MakeProjection<TestEntity>(props);
        Assert.That(
            projectionExpression.ToString(), 
            Is.EqualTo("Param_0 => new TestEntity() {Id = Param_0.Id, SingleSubEntity = new SubEntity() {Id = Param_0.SingleSubEntity.Id, KeyEntityId = Param_0.SingleSubEntity.KeyEntityId}}")
        );
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