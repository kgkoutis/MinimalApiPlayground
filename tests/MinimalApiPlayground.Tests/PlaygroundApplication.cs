using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MinimalApiPlayground.Tests
{
    internal class PlaygroundApplication : WebApplicationFactory<Program>
    {
        private static readonly string _connectionString = "Data Source=testtodos.db";
        private readonly string _environment;

        public PlaygroundApplication(string environment = "Development")
        {
            _environment = environment;
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            EnsureDb();

            builder.UseEnvironment(_environment);

            // Add mock/test services to the builder here
            builder.ConfigureServices(services =>
            {
                services.AddScoped(sp =>
                {
                    // Replace SQLite connection string for tests
                    return new DbContextOptionsBuilder<TodoDb>()
                        .UseSqlite(_connectionString)
                        .UseApplicationServiceProvider(sp)
                        .Options;
                });
            });

            return base.CreateHost(builder);
        }

        private static readonly Lazy<bool> _dbInit = new(() =>
        {
            var dbContextOptions = new DbContextOptionsBuilder<TodoDb>()
                            .UseSqlite(_connectionString)
                            .Options;

            using var db = new TodoDb(dbContextOptions);
            db.Database.EnsureDeleted();
            db.Database.Migrate();

            return true;
        });

        private static void EnsureDb()
        {
            var _ = _dbInit.Value;
        }
    }
}