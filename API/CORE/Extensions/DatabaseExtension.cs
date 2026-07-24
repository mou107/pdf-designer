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

            // EnsureCreated ne cree AUCUNE table sur une base deja existante : on cree donc les
            // nouvelles tables de maniere idempotente (equivalent d'une migration additive, sans
            // toucher aux donnees). A retirer quand les migrations EF seront en place.
            EnsureAdditiveTables(db);
            return builder;
        }

        /// <summary>Cree les tables ajoutees apres la creation initiale (base existante), sans perte de donnees.</summary>
        private static void EnsureAdditiveTables(AppDbContext db)
        {
            if (!db.Database.IsRelational()) return; // InMemory : deja cree par EnsureCreated.

            db.Database.ExecuteSqlRaw(
                "CREATE TABLE IF NOT EXISTS `societe_template_defaults` (" +
                "`Id` varchar(36) NOT NULL," +
                "`SocieteId` varchar(36) NOT NULL," +
                "`DocType` varchar(50) NOT NULL," +
                "`TemplateId` varchar(36) NOT NULL," +
                "`UpdatedAt` datetime(6) NOT NULL," +
                "`UpdatedBy` varchar(36) NULL," +
                "PRIMARY KEY (`Id`)," +
                "UNIQUE KEY `IX_societe_template_defaults_SocieteId_DocType` (`SocieteId`, `DocType`)" +
                ") CHARACTER SET=utf8mb4;");
        }
    }
}
