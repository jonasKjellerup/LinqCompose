using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LinqTools.Tests.EFCore;

public class TestDbContext : DbContext
{

    private TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
    
    public DbSet<PropertyDependencyTestModelA> PropertyDependencyTestModelA { get; set; }
    public DbSet<PropertyDependencyTestModelB> PropertyDependencyTestModelB { get; set; }
    
    public static TestDbContext Create()
    {
        var conn = new SqliteConnection("Filename=./testdb.sqlite");
        conn.Open();

        var contextOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(conn)
            .Options;
        
        var context = new TestDbContext(contextOptions);
        _ = context.Database.EnsureCreated();

        return context;
    }
    
}