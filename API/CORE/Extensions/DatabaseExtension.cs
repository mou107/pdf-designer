using API.DATABASE;
using Microsoft.EntityFrameworkCore;

namespace API.CORE.Extensions
{
    internal static class DatabaseExtension
    {
        internal static WebApplicationBuilder AddDbContext(this WebApplicationBuilder builder)
        {
            var useInMemory = builder.Configuration.GetValue<bool>("Database:UseInMemory");
            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                if (useInMemory)
                {
                    options.UseInMemoryDatabase("pdf_designer");
                }
                else
                {
                    var connectionString = builder.Configuration.GetConnectionString("Default");
                    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                }
            });
            return builder;
        }

        internal static WebApplicationBuilder Migrate(this WebApplicationBuilder builder)
        {
            using var scope = builder.Services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // TODO PR 1.1 : remplacer EnsureCreated par des migrations EF (dotnet ef migrations add Initial)
            db.Database.EnsureCreated();
            return builder;
        }
    }
}
