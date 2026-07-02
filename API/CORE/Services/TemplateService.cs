using API.CORE.Middlewares;
using API.CORE.Schemas;
using API.DATABASE;
using API.DATABASE.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.CORE.Services
{
    /// <summary>
    /// CRUD des modeles de la societe courante + regle "1 defaut par (societe, docType)".
    /// Les seeds globaux (SocieteId null) sont visibles en lecture par toutes les societes.
    /// </summary>
    public class TemplateService
    {
        private readonly AppDbContext _db;
        private readonly TemplateStorageService _storage;
        private readonly TenantContext _tenant;

        public TemplateService(AppDbContext db, TemplateStorageService storage, TenantContext tenant)
        {
            _db = db;
            _storage = storage;
            _tenant = tenant;
        }

        public async Task<List<TemplateModel>> ListAsync(string? docType)
        {
            var query = _db.ReportTemplates
                .Where(t => t.IsActive && (t.SocieteId == _tenant.SocieteId || t.SocieteId == null));

            if (!string.IsNullOrWhiteSpace(docType))
                query = query.Where(t => t.DocType == docType);

            var items = await query.OrderBy(t => t.DocType).ThenByDescending(t => t.IsDefault).ThenBy(t => t.Name).ToListAsync();
            return items.Select(ToModel).ToList();
        }

        public async Task<ReportTemplate?> FindOwnedAsync(string id)
            => await _db.ReportTemplates.FirstOrDefaultAsync(t => t.Id == id && t.SocieteId == _tenant.SocieteId);

        public async Task<ReportTemplate?> FindReadableAsync(string id)
            => await _db.ReportTemplates.FirstOrDefaultAsync(t => t.Id == id && (t.SocieteId == _tenant.SocieteId || t.SocieteId == null));

        /// <summary>Resout le template de rendu : id explicite -> defaut societe -> seed global du docType.</summary>
        public async Task<ReportTemplate?> ResolveForRenderAsync(string docType, string? templateId)
        {
            if (!string.IsNullOrWhiteSpace(templateId))
            {
                var explicitTemplate = await FindReadableAsync(templateId);
                if (explicitTemplate != null) return explicitTemplate;
            }

            var byDefault = await _db.ReportTemplates.FirstOrDefaultAsync(t =>
                t.SocieteId == _tenant.SocieteId && t.DocType == docType && t.IsDefault && t.IsActive);
            if (byDefault != null) return byDefault;

            return await _db.ReportTemplates.FirstOrDefaultAsync(t =>
                t.SocieteId == null && t.DocType == docType && t.IsActive);
        }

        public async Task<TemplateModel> CreateAsync(CreateTemplateRequest request, string seedMrtContent)
        {
            var template = new ReportTemplate
            {
                SocieteId = _tenant.SocieteId,
                DocType = request.DocType,
                Name = string.IsNullOrWhiteSpace(request.Name) ? $"Nouveau modele {request.DocType}" : request.Name,
                CreatedBy = _tenant.UserId,
                UpdatedBy = _tenant.UserId
            };
            template.FilePath = _storage.BuildRelativePath(template.SocieteId, template.DocType, template.Id, template.Version);

            string content = seedMrtContent;
            if (request.From != "seed" && request.From != "blank")
            {
                var source = await FindReadableAsync(request.From)
                    ?? throw new InvalidOperationException($"Template source '{request.From}' introuvable.");
                content = await _storage.ReadAsync(source.FilePath);
            }

            await _storage.WriteAsync(template.FilePath, content);

            // Premier modele du docType pour cette societe -> defaut automatiquement
            var hasDefault = await _db.ReportTemplates.AnyAsync(t =>
                t.SocieteId == _tenant.SocieteId && t.DocType == template.DocType && t.IsDefault && t.IsActive);
            template.IsDefault = !hasDefault;

            _db.ReportTemplates.Add(template);
            _db.ReportTemplateVersions.Add(NewVersion(template));
            await _db.SaveChangesAsync();
            return ToModel(template);
        }

        public async Task<TemplateModel> DuplicateAsync(ReportTemplate source)
        {
            var copy = new ReportTemplate
            {
                SocieteId = _tenant.SocieteId,
                DocType = source.DocType,
                Name = $"{source.Name} (copie)",
                CreatedBy = _tenant.UserId,
                UpdatedBy = _tenant.UserId
            };
            copy.FilePath = _storage.BuildRelativePath(copy.SocieteId, copy.DocType, copy.Id, copy.Version);
            _storage.Copy(source.FilePath, copy.FilePath);

            _db.ReportTemplates.Add(copy);
            _db.ReportTemplateVersions.Add(NewVersion(copy));
            await _db.SaveChangesAsync();
            return ToModel(copy);
        }

        public async Task<TemplateModel> UpdateAsync(ReportTemplate template, UpdateTemplateRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.Name)) template.Name = request.Name;
            if (request.IsActive.HasValue) template.IsActive = request.IsActive.Value;
            template.UpdatedAt = DateTime.UtcNow;
            template.UpdatedBy = _tenant.UserId;
            await _db.SaveChangesAsync();
            return ToModel(template);
        }

        public async Task SetDefaultAsync(ReportTemplate template)
        {
            var currentDefaults = await _db.ReportTemplates
                .Where(t => t.SocieteId == _tenant.SocieteId && t.DocType == template.DocType && t.IsDefault)
                .ToListAsync();
            foreach (var current in currentDefaults) current.IsDefault = false;

            template.IsDefault = true;
            template.UpdatedAt = DateTime.UtcNow;
            template.UpdatedBy = _tenant.UserId;
            await _db.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(ReportTemplate template)
        {
            if (template.IsDefault)
                throw new InvalidOperationException("Impossible de supprimer le modele par defaut. Definissez d'abord un autre modele par defaut.");
            template.IsActive = false;
            template.UpdatedAt = DateTime.UtcNow;
            template.UpdatedBy = _tenant.UserId;
            await _db.SaveChangesAsync();
        }

        /// <summary>Nouvelle version du .mrt apres SaveReport du designer.</summary>
        public async Task<string> SaveNewVersionAsync(ReportTemplate template, string mrtContent)
        {
            template.Version += 1;
            template.FilePath = _storage.BuildRelativePath(template.SocieteId, template.DocType, template.Id, template.Version);
            template.UpdatedAt = DateTime.UtcNow;
            template.UpdatedBy = _tenant.UserId;

            await _storage.WriteAsync(template.FilePath, mrtContent);
            _db.ReportTemplateVersions.Add(NewVersion(template));
            await _db.SaveChangesAsync();
            return template.FilePath;
        }

        public async Task RestoreVersionAsync(ReportTemplate template, int version)
        {
            var archived = await _db.ReportTemplateVersions
                .FirstOrDefaultAsync(v => v.TemplateId == template.Id && v.Version == version)
                ?? throw new InvalidOperationException($"Version {version} introuvable pour ce modele.");

            var content = await _storage.ReadAsync(archived.FilePath);
            await SaveNewVersionAsync(template, content);
        }

        private ReportTemplateVersion NewVersion(ReportTemplate template) => new()
        {
            TemplateId = template.Id,
            Version = template.Version,
            FilePath = template.FilePath,
            CreatedBy = _tenant.UserId
        };

        private static TemplateModel ToModel(ReportTemplate t) => new()
        {
            Id = t.Id,
            SocieteId = t.SocieteId,
            DocType = t.DocType,
            Name = t.Name,
            IsDefault = t.IsDefault,
            IsActive = t.IsActive,
            IsSeed = t.SocieteId == null,
            Version = t.Version,
            UpdatedAt = t.UpdatedAt
        };
    }
}
