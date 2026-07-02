using API.CORE.Schemas;
using API.DATABASE;
using API.DATABASE.Entities;
using Microsoft.EntityFrameworkCore;
using Stimulsoft.Base.Drawing;
using Stimulsoft.Report;
using Stimulsoft.Report.Components;

namespace API.CORE.Services
{
    /// <summary>
    /// Cree au demarrage un modele global (seed) par type de document s'il n'existe pas encore.
    /// Les seeds V1 sont generes par code (page + titre + dictionnaire synchronise sur les donnees exemples) ;
    /// ils seront remplaces par les 7 .mrt recrees fidelement depuis les etalons pdfmake (PR 2.1).
    /// </summary>
    public class SeedService
    {
        private static readonly Dictionary<string, string> Labels = new()
        {
            [DocTypes.Quote] = "Devis",
            [DocTypes.Invoice] = "Facture",
            [DocTypes.CreditNote] = "Avoir",
            [DocTypes.SupplierOrder] = "Bon de commande fournisseur",
            [DocTypes.OperationSheet] = "Fiche d'intervention",
            [DocTypes.MaintenanceOperationSheet] = "Fiche d'intervention maintenance",
            [DocTypes.BonLivraison] = "Bon de livraison"
        };

        private readonly AppDbContext _db;
        private readonly TemplateStorageService _storage;
        private readonly SampleDataService _sampleData;
        private readonly ILogger<SeedService> _logger;

        public SeedService(AppDbContext db, TemplateStorageService storage, SampleDataService sampleData, ILogger<SeedService> logger)
        {
            _db = db;
            _storage = storage;
            _sampleData = sampleData;
            _logger = logger;
        }

        public async Task EnsureSeedsAsync()
        {
            foreach (var docType in DocTypes.All)
            {
                var exists = await _db.ReportTemplates.AnyAsync(t => t.SocieteId == null && t.DocType == docType);
                if (exists) continue;

                var template = new ReportTemplate
                {
                    SocieteId = null,
                    DocType = docType,
                    Name = $"Modele standard — {Labels[docType]}",
                    IsDefault = false
                };
                template.FilePath = _storage.BuildRelativePath(null, docType, template.Id, template.Version);

                var mrt = await BuildSeedMrtAsync(docType);
                await _storage.WriteAsync(template.FilePath, mrt);

                _db.ReportTemplates.Add(template);
                _db.ReportTemplateVersions.Add(new ReportTemplateVersion
                {
                    TemplateId = template.Id,
                    Version = template.Version,
                    FilePath = template.FilePath
                });

                _logger.LogInformation("Seed cree pour {DocType} : {Path}", docType, template.FilePath);
            }

            await _db.SaveChangesAsync();
        }

        public async Task<string> GetSeedMrtContentAsync(string docType)
        {
            var seed = await _db.ReportTemplates.FirstOrDefaultAsync(t => t.SocieteId == null && t.DocType == docType);
            if (seed != null && _storage.Exists(seed.FilePath))
                return await _storage.ReadAsync(seed.FilePath);
            return await BuildSeedMrtAsync(docType);
        }

        public async Task<string> BuildBlankMrtAsync(string docType)
        {
            var report = StiReport.CreateNewReport();
            report.ReportName = $"{Labels[docType]} — vierge";
            RenderService.RegisterData(report, await _sampleData.GetSampleDataAsync(docType));
            return report.SaveToString();
        }

        private async Task<string> BuildSeedMrtAsync(string docType)
        {
            var report = StiReport.CreateNewReport();
            report.ReportName = $"{Labels[docType]} — modele standard";

            var page = report.Pages[0];

            var title = new StiText(new RectangleD(0, 0.5, page.Width, 1.2))
            {
                Name = "TitreDocument",
                HorAlignment = StiTextHorAlignment.Center
            };
            title.Text.Value = Labels[docType].ToUpperInvariant();
            page.Components.Add(title);

            var hint = new StiText(new RectangleD(0, 2.0, page.Width, 0.8))
            {
                Name = "AideConception",
                HorAlignment = StiTextHorAlignment.Center
            };
            hint.Text.Value = "Modele de depart — utilisez le dictionnaire de donnees (document, societe, options) pour placer vos champs.";
            page.Components.Add(hint);

            // Le dictionnaire est synchronise sur les donnees exemples : l'utilisateur voit
            // les champs disponibles (document, societe, options) dans le designer.
            RenderService.RegisterData(report, await _sampleData.GetSampleDataAsync(docType));

            return report.SaveToString();
        }
    }
}
