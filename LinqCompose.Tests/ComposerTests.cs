using System.Linq.Expressions;
using LinqCompose;

namespace LinqTools.Tests;

public class ComposerTests
{
    [Test]
    public void Inline_SimpleLambda()
    {
        Expression<Func<double, double>> expr = x => x * 2;
        var composition = Composer.Compose<Func<double, double>>(v => Composer.Inline(expr)(v) * 2);

        Assert.That(
            composition.ToString(),
            Is.EqualTo("v => ((v * 2) * 2)")
        );
    }
    
    [Test]
    public void Inline_SimpleLambda_NonStaticExpression()
    {
        Expression<Func<double, double>> expr = x => x * 2;
        var composition = Composer.Compose<Func<double, double>>(v => Composer.Inline(expr)(v) * 2);
        var composition2 = Composer.Compose<Func<double, double>>(x => Composer.Inline(composition)(x) * 2);

        Assert.That(
            composition2.ToString(),
            Is.EqualTo("x => (((x * 2) * 2) * 2)")
        );
    }
    
    [Test]
    public void Inline_SimpleLambda_PassedThroughParameter()
    {
        Expression<Func<double, double>> expr = x => x * 2;

        Assert.That(
            SubstituteExpr(expr),
            Is.EqualTo("v => (v * 2)")
        );
        return;

        string SubstituteExpr(Expression<Func<double, double>> expression)
        {
            var composition = Composer.Compose<Func<double, double>>(v => Composer.Inline(expression)(v));
            return composition.ToString();
        }
    }

    [Test]
    public void Inline_MultiParameter()
    {
        Expression<Func<double, double, int, double>> expr = (x, y, z) => x / y * 2 + 11;
        var composition =
            Composer.Compose<Func<double, double>>(v => Composer.Inline(expr)(v, v * Math.PI + 45, 12) * 2);

        Assert.That(
            composition.ToString(),
            Is.EqualTo(
                "v => ((((v / ((v * 3,141592653589793) + 45)) * 2) + 11) * 2)")
        );
    }
    
    [Test]
    public void Substitute_InlineLambda()
    {
        var composition = Composer.Compose<Func<double, double>>(v => Composer.Inline((Expression<Func<double, double>>)(x => x * 2))(v));

        Assert.That(
            composition.ToString(),
            Is.EqualTo("v => (v * 2)")
        );
    }

    [Test]
    public void Inline_ConstantExpression()
    {
        var expr = Expression.Constant(1.0, typeof(double));
        var composition = Composer.Compose<Func<double, double>>(v => v + Composer.Inline<double>(expr));

        Assert.That(
            composition.ToString(),
            Is.EqualTo("v => (v + 1)")
        );
    }

    [Test]
    public void Inline_InlineConstantExpression()
    {
        var composition = Composer.Compose<Func<double, double>>(v =>
            v + Composer.Inline<double>(Expression.Constant(1.0, typeof(double))));

        Assert.That(
            composition.ToString(),
            Is.EqualTo("v => (v + 1)")
        );
    }

    [Test]
    public void Inline_ParameterReferenceInExpression_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = Composer.Compose<Func<double, double>>(v =>
                v + Composer.Inline<double>(Expression.Constant(v, typeof(double))));
        });
    }

    [Test]
    public void Inline_LocalVariable_UsedAsMethodArgument()
    {
        Expression<Func<double, bool>> pred = x => x > 0;
        var composition = Composer.Compose<IEnumerable<double>, IEnumerable<double>>(
            source => source.Where(Composer.Inline(pred)));

        Assert.That(
            composition.ToString(),
            Is.EqualTo("source => source.Where(x => (x > 0))")
        );
    }

    [Test]
    public void Inline_LocalVariable_UsedAsNewArrayElement()
    {
        Expression<Func<double, double>> expr = x => x * 2;
        var composition = Composer.Compose<Func<Func<double, double>[]>>(
            () => new[] { Composer.Inline(expr) });

        Assert.That(
            composition.ToString(),
            Is.EqualTo("() => new [] {x => (x * 2)}")
        );
    }

    [Test]
    public void Compose_NonComposerMethod_DoesNothing()
    {
        var composition = Composer.Compose<Func<int, int>>(v => Increment(v));
        
        Assert.That(
            composition.ToString(),
            Is.EqualTo("v => value(LinqTools.Tests.ComposerTests).Increment(v)"));
    }
    
    private int Increment(int i)
    {
        return i + 1;
    }
    
}