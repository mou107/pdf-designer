using API.DATABASE.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.DATABASE
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<ReportTemplate> ReportTemplates => Set<ReportTemplate>();
        public DbSet<ReportTemplateVersion> ReportTemplateVersions => Set<ReportTemplateVersion>();
        public DbSet<SocieteDefaultTemplate> SocieteDefaultTemplates => Set<SocieteDefaultTemplate>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ReportTemplate>()
                .HasIndex(t => new { t.SocieteId, t.DocType });

            modelBuilder.Entity<ReportTemplateVersion>()
                .HasIndex(v => v.TemplateId);

            // Un seul modele par defaut par (societe, type de document).
            modelBuilder.Entity<SocieteDefaultTemplate>()
                .HasIndex(d => new { d.SocieteId, d.DocType })
                .IsUnique();
        }
    }
}
