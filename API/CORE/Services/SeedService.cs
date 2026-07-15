using API.CORE.Schemas;
using API.DATABASE;
using API.DATABASE.Entities;
using Microsoft.EntityFrameworkCore;
using Stimulsoft.Report;

namespace API.CORE.Services
{
    /// <summary>
    /// Cree au demarrage les 7 modeles globaux (seeds) par type de document — repliques V1
    /// des 7 modeles pdfmake historiques (voir SeedTemplateFactory). La parite fine avec
    /// les etalons pdfmake sera peaufinee dans le designer (PR 2.1).
    /// </summary>
    public class SeedService
    {
        public const int ModelCount = 7;

        private static readonly Dictionary<string, string> Labels = new()
        {
            [DocTypes.Quote] = "Devis",
            [DocTypes.Invoice] = "Facture",
            [DocTypes.CreditNote] = "Avoir",
            [DocTypes.SupplierOrder] = "Bon de commande fournisseur",
            [DocTypes.OperationSheet] = "Fiche d'intervention",
            [DocTypes.MaintenanceOperationSheet] = "Fiche d'intervention maintenance",
            [DocTypes.BonLivraison] = "Bon de livraison",
            [DocTypes.WorksiteSheet] = "Fiche chantier",
            [DocTypes.CustomerSheet] = "Fiche client / prospect",
            [DocTypes.DealSheet] = "Fiche affaire",
            [DocTypes.TimeSheet] = "Releve d'heures"
        };

        private readonly AppDbContext _db;
        private readonly TemplateStorageService _storage;
        private readonly ILogger<SeedService> _logger;

        public SeedService(AppDbContext db, TemplateStorageService storage, ILogger<SeedService> logger)
        {
            _db = db;
            _storage = storage;
            _logger = logger;
        }

        public async Task EnsureSeedsAsync()
        {
            foreach (var docType in DocTypes.All)
            {
                var existing = await _db.ReportTemplates
                    .Where(t => t.SocieteId == null && t.DocType == docType)
                    .Select(t => t.Name)
                    .ToListAsync();

                var sampleJson = SampleSkeleton.GetJson(docType);
                var maxModel = DocTypes.SingleModel.Contains(docType) ? 1 : ModelCount;

                for (var model = 1; model <= maxModel; model++)
                {
                    var name = maxModel == 1 ? "Standard" : $"Modele {model}";
                    if (existing.Contains(name)) continue;

                    var template = new ReportTemplate
                    {
                        SocieteId = null,
                        DocType = docType,
                        Name = name,
                        Model = model,
                        ConfigJson = TemplateService.DefaultConfigJson,
                        FileNamePattern = "{TypeDocument}_{Ref}",
                        IsDefault = false
                    };
                    template.FilePath = _storage.BuildRelativePath(null, docType, template.Id, template.Version);

                    var report = SeedTemplateFactory.Build(model, docType, Labels[docType], sampleJson);
                    await _storage.WriteAsync(template.FilePath, report.SaveToString());

                    _db.ReportTemplates.Add(template);
                    _db.ReportTemplateVersions.Add(new ReportTemplateVersion
                    {
                        TemplateId = template.Id,
                        Version = template.Version,
                        FilePath = template.FilePath
                    });

                    _logger.LogInformation("Seed cree : {DocType} / {Name} -> {Path}", docType, name, template.FilePath);
                }
            }

            await _db.SaveChangesAsync();
        }

        /// <summary>Contenu .mrt de depart pour la creation d'un modele societe ("from": "seed") — modele demande (1-7).</summary>
        public async Task<string> GetSeedMrtContentAsync(string docType, int model = 1)
        {
            if (model < 1 || model > ModelCount) model = 1;

            var seed = await _db.ReportTemplates
                .Where(t => t.SocieteId == null && t.DocType == docType && t.Model == model)
                .FirstOrDefaultAsync();

            if (seed != null && _storage.Exists(seed.FilePath))
                return await _storage.ReadAsync(seed.FilePath);

            var report = SeedTemplateFactory.Build(model, docType, Labels[docType], SampleSkeleton.GetJson(docType));
            return report.SaveToString();
        }

        public Task<string> BuildBlankMrtAsync(string docType)
        {
            var report = StiReport.CreateNewReport();
            report.ReportName = $"{Labels[docType]} — vierge";
            RenderService.RegisterData(report, SampleSkeleton.GetJson(docType));
            return Task.FromResult(report.SaveToString());
        }
    }
}
