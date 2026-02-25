using System.Reflection;
using LinqCompose;

namespace LinqTools.Tests;

#pragma warning disable CS8618
// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
// ReSharper disable once UnusedMember.Local

public class DependencySetBuilderTests
{

    [Test]
    public void InvalidPathIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            var props = typeof(TestClassA).GetProperties(BindingFlags.Instance | BindingFlags.Public);

            new DependencySetBuilder(true)
                .AddPropertyPath(props);
        });
    }
    
    [Test]
    public void ValidPathIsNotRejected()
    {
        var propAb = typeof(TestClassA)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .First(p => p.Name == nameof(TestClassA.B));
        
        var propBc = typeof(TestClassB)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .First(p => p.Name == nameof(TestClassB.C));

        new DependencySetBuilder(true)
            .AddPropertyPath(propAb, propBc);
        
        Assert.Pass();
    }
    
    [Test]
    public void ValidPathWithInheritedMemberIsNotRejected()
    {
        var propAb = typeof(TestClassA)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .First(p => p.Name == nameof(TestClassA.B));
        
        var propBc = typeof(TestClassB)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .First(p => p.Name == nameof(TestClassB.C));

        new DependencySetBuilder(true)
            .AddPropertyPath(propBc, propAb);
        
        Assert.Pass();
    }
    
    [Test]
    public void ValidPathGeneratesValidProjection()
    {
        var propAb = typeof(TestClassA)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .First(p => p.Name == nameof(TestClassA.B));
        
        var propBc = typeof(TestClassB)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .First(p => p.Name == nameof(TestClassB.C));

        var tree = new DependencySetBuilder(true)
            .AddPropertyPath(propBc, propAb)
            .Result();
        
        var projection = ProjectionBuilder.MakeProjection<TestClassB>(tree);
        Assert.That(projection.ToString(), Is.EqualTo("Param_0 => new TestClassB() {C = new TestClassC() {B = Param_0.C.B}}"));
    }
    
    private class TestClassA
    {
        public TestClassB B { get; set; }
        public TestClassC C { get; set; }
    }

    private class TestClassB
    {
        public TestClassC C { get; set; }
    }

    private class TestClassC : TestClassA
    {
        
    }

}

