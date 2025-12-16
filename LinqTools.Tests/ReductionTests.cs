using System.Linq.Expressions;

namespace LinqTools.Tests;

public class ReductionTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void ConstantApplication()
    {
        Expression<Func<int, int>> a = x => x + 10;
        var visitor = new ReductionVisitor(a.Parameters.First(), Expression.Constant(20));
        
        var result = visitor.Visit(a);
        Assert.Multiple(() =>
        {
            Assert.That(result.NodeType, Is.EqualTo(ExpressionType.Add));
            Assert.That(result.ToString(), Is.EqualTo("(20 + 10)"));
        });
    }

    [Test]
    public void LambdaApplication()
    {
        Expression<Func<Func<int, int, int>, int>> a = x => x(10, 30);
        Expression<Func<int, int, int>> b = (x, y) => x * y;
        var reductionVisitor = new ReductionVisitor(a.Parameters.First(), b);
        
        var result = reductionVisitor.Visit(a);
        Assert.Multiple(() =>
        {
            Assert.That(result.NodeType, Is.EqualTo(ExpressionType.Multiply));
            Assert.That(result.ToString(), Is.EqualTo("(10 * 30)"));
        });
    }
    
    [Test]
    public void LambdaApplication_MultiParameter()
    {
        Expression<Func<Func<int, int, int>, double, double>> a = (x, y) => x(10, 400) / Math.Round(y);
        Expression<Func<int, int, int>> b = (x, y) => x - y;
        var reductionVisitor = new ReductionVisitor(a.Parameters.First(), b);
        
        var result = reductionVisitor.Visit(a);
        Assert.Multiple(() =>
        {
            Assert.That(result is Expression<Func<double, double>>);
            Assert.That(result.ToString(), Is.EqualTo("y => (Convert((10 - 400), Double) / Round(y))"));
            var fn = ((Expression<Func<double, double>>)result).Compile();
            Assert.That(fn(1.0), Is.EqualTo(-390.0));
        });
    }
    
}