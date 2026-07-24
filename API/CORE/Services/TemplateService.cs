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
            // La bibliotheque affiche les templates de la societe ET les modeles standards Foliatech
            // (seeds globaux, SocieteId null) pour que l'utilisateur puisse choisir/dupliquer les 7 modeles.
            var query = _db.ReportTemplates
                .Where(t => t.IsActive && (t.SocieteId == _tenant.SocieteId || t.SocieteId == null));

            if (!string.IsNullOrWhiteSpace(docType))
                query = query.Where(t => t.DocType == docType);

            var items = await query.ToListAsync();

            // Pointeurs "par defaut" de la societe (peuvent viser un seed) : source de verite du defaut.
            var defaultIds = await ResolveDefaultTemplateIdsAsync(items, docType);

            // Ordre : le defaut d'abord (meme si c'est un seed), puis templates de la societe,
            // puis les modeles standards par numero.
            var ordered = items
                .OrderByDescending(t => defaultIds.Contains(t.Id))
                .ThenBy(t => t.SocieteId == null ? 1 : 0)
                .ThenBy(t => t.SocieteId == null ? t.Model : 0)
                .ThenBy(t => t.Name)
                .ToList();

            return ordered.Select(t => ToModel(t, defaultIds.Contains(t.Id))).ToList();
        }

        /// <summary>
        /// Determine, par type de document, l'Id du template par defaut de la societe : pointeur explicite
        /// (societe ou seed) si present, sinon repli sur l'ancien flag IsDefault d'un template de la societe.
        /// </summary>
        private async Task<HashSet<string>> ResolveDefaultTemplateIdsAsync(List<ReportTemplate> items, string? docType)
        {
            var pointerQuery = _db.SocieteDefaultTemplates.Where(p => p.SocieteId == _tenant.SocieteId);
            if (!string.IsNullOrWhiteSpace(docType))
                pointerQuery = pointerQuery.Where(p => p.DocType == docType);
            var pointers = (await pointerQuery.ToListAsync())
                .GroupBy(p => p.DocType)
                .ToDictionary(g => g.Key, g => g.First().TemplateId);

            var result = new HashSet<string>();
            foreach (var group in items.GroupBy(t => t.DocType))
            {
                if (pointers.TryGetValue(group.Key, out var pointedId) && group.Any(t => t.Id == pointedId))
                {
                    result.Add(pointedId);
                }
                else
                {
                    // Retrocompatibilite : pas de pointeur -> ancien flag IsDefault d'un template societe.
                    var legacy = group.FirstOrDefault(t => t.SocieteId == _tenant.SocieteId && t.IsDefault && t.IsActive);
                    if (legacy != null) result.Add(legacy.Id);
                }
            }
            return result;
        }

        public async Task<ReportTemplate?> FindOwnedAsync(string id)
            => await _db.ReportTemplates.FirstOrDefaultAsync(t => t.Id == id && t.SocieteId == _tenant.SocieteId);

        public async Task<ReportTemplate?> FindReadableAsync(string id)
            => await _db.ReportTemplates.FirstOrDefaultAsync(t => t.Id == id && (t.SocieteId == _tenant.SocieteId || t.SocieteId == null));

        /// <summary>Seed global (modele standard) pour (docType, model) — pour l'apercu d'un autre modele.</summary>
        public async Task<ReportTemplate?> FindSeedAsync(string docType, int model)
            => await _db.ReportTemplates.FirstOrDefaultAsync(t => t.SocieteId == null && t.DocType == docType && t.Model == model && t.IsActive);

        /// <summary>Chemin absolu du .mrt d'un template (pour le rendu direct).</summary>
        public string GetAbsolutePath(ReportTemplate template) => _storage.GetAbsolutePath(template.FilePath);

        /// <summary>
        /// Resout le template de rendu : id explicite -> pointeur "par defaut" de la societe (societe OU seed)
        /// -> ancien flag IsDefault (retrocompat) -> seed global du docType.
        /// </summary>
        public async Task<ReportTemplate?> ResolveForRenderAsync(string docType, string? templateId)
        {
            if (!string.IsNullOrWhiteSpace(templateId))
            {
                var explicitTemplate = await FindReadableAsync(templateId);
                if (explicitTemplate != null) return explicitTemplate;
            }

            // Pointeur "par defaut" de la societe : peut viser un modele societe ou un seed global.
            var pointer = await _db.SocieteDefaultTemplates
                .FirstOrDefaultAsync(p => p.SocieteId == _tenant.SocieteId && p.DocType == docType);
            if (pointer != null)
            {
                var pointed = await _db.ReportTemplates.FirstOrDefaultAsync(t =>
                    t.Id == pointer.TemplateId && t.IsActive
                    && (t.SocieteId == _tenant.SocieteId || t.SocieteId == null));
                if (pointed != null) return pointed;
            }

            // Retrocompatibilite : ancien defaut porte par le flag IsDefault d'un template societe.
            var byDefault = await _db.ReportTemplates.FirstOrDefaultAsync(t =>
                t.SocieteId == _tenant.SocieteId && t.DocType == docType && t.IsDefault && t.IsActive);
            if (byDefault != null) return byDefault;

            return await _db.ReportTemplates
                .Where(t => t.SocieteId == null && t.DocType == docType && t.IsActive)
                .OrderBy(t => t.Model)
                .FirstOrDefaultAsync();
        }

        public async Task<TemplateModel> CreateAsync(CreateTemplateRequest request, string seedMrtContent)
        {
            var template = new ReportTemplate
            {
                SocieteId = _tenant.SocieteId,
                DocType = request.DocType,
                Name = string.IsNullOrWhiteSpace(request.Name) ? $"Nouveau modele {request.DocType}" : request.Name,
                Description = request.Description,
                Model = request.Model is >= 1 and <= 7 ? request.Model : 1,
                TableStyle = 2,
                ConfigJson = DefaultConfigJson,
                FileNamePattern = "{TypeDocument}_{Ref}",
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

            // Defaut automatique uniquement si la societe n'a AUCUN defaut pour ce docType : ni pointeur
            // (modele societe ou seed choisi explicitement), ni ancien flag IsDefault.
            var hasPointer = await _db.SocieteDefaultTemplates
                .AnyAsync(p => p.SocieteId == _tenant.SocieteId && p.DocType == template.DocType);
            var hasLegacyDefault = await _db.ReportTemplates
                .AnyAsync(t => t.SocieteId == _tenant.SocieteId && t.DocType == template.DocType && t.IsDefault && t.IsActive);
            template.IsDefault = !hasPointer && !hasLegacyDefault;

            _db.ReportTemplates.Add(template);
            _db.ReportTemplateVersions.Add(NewVersion(template));
            if (template.IsDefault)
            {
                _db.SocieteDefaultTemplates.Add(new SocieteDefaultTemplate
                {
                    SocieteId = _tenant.SocieteId!,
                    DocType = template.DocType,
                    TemplateId = template.Id,
                    UpdatedBy = _tenant.UserId
                });
            }
            await _db.SaveChangesAsync();
            return ToModel(template, template.IsDefault);
        }

        public async Task<TemplateModel> DuplicateAsync(ReportTemplate source)
        {
            var copy = new ReportTemplate
            {
                SocieteId = _tenant.SocieteId,
                DocType = source.DocType,
                Name = $"{source.Name} (copie)",
                Description = source.Description,
                Model = source.Model,
                TableStyle = source.TableStyle,
                ConfigJson = source.ConfigJson ?? DefaultConfigJson,
                FileNamePattern = source.FileNamePattern,
                IsDesignerCustomized = source.IsDesignerCustomized,
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
            if (request.Description != null) template.Description = request.Description;
            if (request.FileNamePattern != null) template.FileNamePattern = request.FileNamePattern;
            if (request.IsActive.HasValue) template.IsActive = request.IsActive.Value;
            template.UpdatedAt = DateTime.UtcNow;
            template.UpdatedBy = _tenant.UserId;
            await _db.SaveChangesAsync();
            return ToModel(template);
        }

        /// <summary>Sauvegarde de la config simple depuis l'ecran de parametrage : bump de version + snapshot.</summary>
        public async Task<TemplateModel> SaveConfigAsync(ReportTemplate template, SaveConfigRequest request)
        {
            var modelChanged = request.Model is >= 1 and <= 7 && request.Model.Value != template.Model;

            if (request.Model is >= 1 and <= 7) template.Model = request.Model.Value;
            if (request.TableStyle is >= 1 and <= 3) template.TableStyle = request.TableStyle.Value;
            if (request.ConfigJson != null) template.ConfigJson = request.ConfigJson;
            if (request.FileNamePattern != null) template.FileNamePattern = request.FileNamePattern;

            var oldPath = template.FilePath;
            template.Version += 1;
            template.UpdatedAt = DateTime.UtcNow;
            template.UpdatedBy = _tenant.UserId;
            template.MasterUpdateAvailable = false;

            // Changement de modele : on recopie la mise en page (.mrt) du modele standard choisi (1-7).
            // Sinon le .mrt est inchange (la config simple est injectee en variables au rendu).
            if (modelChanged)
            {
                var seed = await _db.ReportTemplates.FirstOrDefaultAsync(t =>
                    t.SocieteId == null && t.DocType == template.DocType && t.Model == template.Model && t.IsActive);

                template.FilePath = _storage.BuildRelativePath(template.SocieteId, template.DocType, template.Id, template.Version);
                var content = seed != null && _storage.Exists(seed.FilePath)
                    ? await _storage.ReadAsync(seed.FilePath)
                    : await _storage.ReadAsync(oldPath);
                await _storage.WriteAsync(template.FilePath, content);
            }

            _db.ReportTemplateVersions.Add(NewVersion(template));
            await _db.SaveChangesAsync();
            return ToModel(template);
        }

        /// <summary>Applique le modele + config du template source au template PAR DEFAUT de chaque autre docType (parite "UpdateAll").</summary>
        public async Task<(int applied, List<string> skipped)> ApplyToAllDocTypesAsync(ReportTemplate source)
        {
            var defaults = await _db.ReportTemplates
                .Where(t => t.SocieteId == _tenant.SocieteId && t.IsDefault && t.IsActive && t.DocType != source.DocType)
                .ToListAsync();

            var applied = 0;
            var skipped = new List<string>();
            foreach (var def in defaults)
            {
                if (def.IsDesignerCustomized) { skipped.Add(def.DocType); continue; }
                def.Model = DocTypes.SingleModel.Contains(def.DocType) ? 1 : source.Model;
                def.TableStyle = source.TableStyle;
                def.ConfigJson = source.ConfigJson;
                def.Version += 1;
                def.UpdatedAt = DateTime.UtcNow;
                def.UpdatedBy = _tenant.UserId;
                _db.ReportTemplateVersions.Add(NewVersion(def));
                applied++;
            }
            await _db.SaveChangesAsync();
            return (applied, skipped);
        }

        /// <summary>
        /// Definit le modele par defaut de la societe pour un type de document. Le template peut etre
        /// un modele de la societe OU un modele standard global (seed) — aucune duplication requise.
        /// Le choix est porte par un pointeur (societe, docType) ; l'ancien flag IsDefault des modeles
        /// societe est maintenu synchronise pour la retrocompatibilite.
        /// </summary>
        public async Task SetDefaultAsync(ReportTemplate template)
        {
            if (string.IsNullOrWhiteSpace(_tenant.SocieteId))
                throw new InvalidOperationException("Aucune societe courante : impossible de definir un modele par defaut.");

            // Upsert du pointeur (un seul defaut par societe + docType).
            var pointer = await _db.SocieteDefaultTemplates
                .FirstOrDefaultAsync(p => p.SocieteId == _tenant.SocieteId && p.DocType == template.DocType);
            if (pointer == null)
            {
                pointer = new SocieteDefaultTemplate { SocieteId = _tenant.SocieteId!, DocType = template.DocType };
                _db.SocieteDefaultTemplates.Add(pointer);
            }
            pointer.TemplateId = template.Id;
            pointer.UpdatedAt = DateTime.UtcNow;
            pointer.UpdatedBy = _tenant.UserId;

            // Synchronise l'ancien flag IsDefault sur les modeles de la societe : vrai uniquement si le
            // defaut choisi est un modele de la societe (un seed reste global, jamais marque en base).
            var owned = await _db.ReportTemplates
                .Where(t => t.SocieteId == _tenant.SocieteId && t.DocType == template.DocType)
                .ToListAsync();
            foreach (var t in owned) t.IsDefault = t.Id == template.Id;

            await _db.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(ReportTemplate template)
        {
            var isPointedDefault = await _db.SocieteDefaultTemplates.AnyAsync(p =>
                p.SocieteId == _tenant.SocieteId && p.DocType == template.DocType && p.TemplateId == template.Id);
            if (template.IsDefault || isPointedDefault)
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
            // Toute sauvegarde depuis le designer rend le template "personnalise" (config simple desactivee).
            template.IsDesignerCustomized = true;

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

        /// <param name="isDefault">Defaut effectif (pointeur societe/seed) ; a defaut, le flag stocke.</param>
        private static TemplateModel ToModel(ReportTemplate t, bool? isDefault = null) => new()
        {
            Id = t.Id,
            SocieteId = t.SocieteId,
            DocType = t.DocType,
            Name = t.Name,
            Description = t.Description,
            IsDefault = isDefault ?? t.IsDefault,
            IsActive = t.IsActive,
            IsSeed = t.SocieteId == null,
            Model = t.Model,
            TableStyle = t.TableStyle,
            ConfigJson = t.ConfigJson,
            FileNamePattern = t.FileNamePattern,
            IsDesignerCustomized = t.IsDesignerCustomized,
            MasterUpdateAvailable = t.MasterUpdateAvailable,
            Version = t.Version,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            CreatedBy = t.CreatedBy,
            UpdatedBy = t.UpdatedBy
        };

        /// <summary>Config simple par defaut (couleur principale, colonnes, styles de texte) — parite ecran actuel.</summary>
        public const string DefaultConfigJson =
            "{\"colors\":{\"main\":\"#47a2c1\",\"header\":\"#47a2c1\",\"total\":\"#47a2c1\",\"alt1\":\"#F1F6F8\"}," +
            "\"cols\":{\"num\":false,\"vignette\":false,\"qte\":true,\"unite\":true,\"prixU\":true,\"tva\":true,\"prixHT\":true,\"ttc\":true}," +
            "\"lineStyles\":{" +
            "\"article\":{\"size\":10,\"bold\":true,\"italic\":false,\"underline\":false,\"color\":\"#333333\"}," +
            "\"ouvrage\":{\"size\":9,\"bold\":true,\"italic\":false,\"underline\":false,\"color\":\"#C0392B\"}," +
            "\"sousOuvrage\":{\"size\":9,\"bold\":false,\"italic\":true,\"underline\":false,\"color\":\"#333333\"}," +
            "\"lot\":{\"size\":12,\"bold\":true,\"italic\":false,\"underline\":false,\"color\":\"#C0392B\"}," +
            "\"ligne\":{\"size\":9,\"bold\":false,\"italic\":false,\"underline\":false,\"color\":\"#333333\"}}}";
    }
}
