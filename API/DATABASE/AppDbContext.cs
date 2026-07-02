using API.DATABASE.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.DATABASE
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<ReportTemplate> ReportTemplates => Set<ReportTemplate>();
        public DbSet<ReportTemplateVersion> ReportTemplateVersions => Set<ReportTemplateVersion>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ReportTemplate>()
                .HasIndex(t => new { t.SocieteId, t.DocType });

            modelBuilder.Entity<ReportTemplateVersion>()
                .HasIndex(v => v.TemplateId);
        }
    }
}
