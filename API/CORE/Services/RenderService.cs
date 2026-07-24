using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using API.CORE.Middlewares;
using API.CORE.Schemas;
using API.DATABASE.Entities;
using Stimulsoft.Base;
using Stimulsoft.Report;

namespace API.CORE.Services
{
    /// <summary>
    /// Rendu PDF serveur : template .mrt + donnees JSON -> PDF.
    /// Limite de concurrence + timeout configurables (Render:MaxConcurrency / Render:TimeoutSeconds).
    /// </summary>
    public class RenderService
    {
        private static SemaphoreSlim? _semaphore;
        private readonly TemplateStorageService _storage;
        private readonly SocieteAssetsService _assets;
        private readonly TenantContext _tenant;
        private readonly ILogger<RenderService> _logger;
        private readonly int _timeoutSeconds;

        public RenderService(TemplateStorageService storage, SocieteAssetsService assets, TenantContext tenant, IConfiguration configuration, ILogger<RenderService> logger)
        {
            _storage = storage;
            _assets = assets;
            _tenant = tenant;
            _logger = logger;
            _timeoutSeconds = configuration.GetValue("Render:TimeoutSeconds", 60);
            _semaphore ??= new SemaphoreSlim(Math.Max(1, configuration.GetValue("Render:MaxConcurrency", 4)));
        }

        public async Task<byte[]> RenderPdfAsync(ReportTemplate template, PdfRenderPayload payload)
        {
            var json = BuildDataJson(payload.Document, payload.Societe, payload.Options);

            // Assets fournis dans la requete -> rendu sans etat (aucune lecture du cache memoire).
            var a = payload.Assets;
            if (a != null)
                return await RenderFileAsync(_storage.GetAbsolutePath(template.FilePath), template.SocieteId, json,
                    template.ConfigJson, template.TableStyle,
                    overrideAssets: true, logoOverride: a.Logo, cachetOverride: a.Cachet,
                    backgroundOverride: a.Background, mainColorOverride: a.MainColor);

            return await RenderPdfAsync(template, json);
        }

        /// <param name="configOverride">Config simple (JSON) a appliquer ; a defaut, celle du template.</param>
        public Task<byte[]> RenderPdfAsync(ReportTemplate template, string dataJson, string? configOverride = null)
            => RenderFileAsync(_storage.GetAbsolutePath(template.FilePath), template.SocieteId, dataJson, configOverride ?? template.ConfigJson, template.TableStyle);

        /// <summary>Rend un .mrt arbitraire (chemin absolu) avec le contexte societe (logo, couleur, style de tableau).</summary>
        public async Task<byte[]> RenderFileAsync(string mrtAbsolutePath, string? societeId, string dataJson, string? configJson, int tableStyle = 0,
            double? logoWidthPx = null, double? logoHeightPx = null, double? cachetWidthPx = null, double? cachetHeightPx = null,
            bool overrideAssets = false, string? logoOverride = null, string? cachetOverride = null, bool asPng = false,
            string? backgroundOverride = null, string? mainColorOverride = null)
        {
            await _semaphore!.WaitAsync(TimeSpan.FromSeconds(_timeoutSeconds));
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var report = StiReport.CreateNewReport();
                report.Load(mrtAbsolutePath);

                // Styles de texte par type de ligne (designationHtml) + options « Divers » (bloc client, affaire,
                // adresse intervention) construits dans les donnees selon la config.
                var styledData = DataStyler.AddDesignationHtml(dataJson, configJson);
                styledData = DiversStyler.Apply(styledData, configJson);
                // Vignette article : telecharge les URL d'images cote serveur et les convertit en base64.
                styledData = await VignetteResolver.ResolveAsync(styledData);
                // Titre / sous-titre configures (« Divers > Textes ») + resolution des tags #reference/#type/#compteur.
                styledData = TextsStyler.Apply(styledData, configJson);
                RegisterData(report, styledData);

                // Assets (logo, cachet, papier entete, couleur). Cible du refactor « microservice neutre » :
                //  - overrideAssets=true  -> tout vient de la REQUETE (logo/cachet/fond/couleur en base64/hex) ;
                //                            le rendu ne depend d'AUCUN etat serveur.
                //  - overrideAssets=false -> repli sur le cache memoire par societe (encore utilise par le
                //                            designer ; sera retire en Phase 3).
                string? mainColor;
                if (overrideAssets)
                {
                    SocieteAssetsService.ApplyAssets(report, logoOverride, cachetOverride);
                    SocieteAssetsService.ApplyBackground(report, backgroundOverride);
                    mainColor = mainColorOverride;
                }
                else
                {
                    var assetSocieteId = string.IsNullOrWhiteSpace(_tenant.SocieteId) ? societeId : _tenant.SocieteId;
                    _assets.Apply(report, assetSocieteId);
                    mainColor = _assets.Get(assetSocieteId)?.MainColor;
                }
                // Dimensions logo/cachet (px, config du template) : redimensionnent les boites apres les avoir alimentees.
                SocieteAssetsService.ApplyImageDimensions(report, SocieteAssetsService.LogoComponentName, logoWidthPx, logoHeightPx);
                SocieteAssetsService.ApplyImageDimensions(report, SocieteAssetsService.CachetComponentName, cachetWidthPx, cachetHeightPx);
                // Couleur : config explicite, sinon couleur societe (requete en override, sinon cache).
                PdfConfigApplier.Apply(report, configJson, mainColor);
                // Visibilite des colonnes du tableau (config `cols`) : masque + recompacte avant le style de tableau.
                ColumnsApplier.Apply(report, configJson);
                // Options « Divers » : normalise les bindings identite client vers les variables calculees,
                // pour que Divers pilote le bloc client meme sur un .mrt personnalise dans l'editeur avance.
                DiversApplier.Apply(report);
                TableStyleApplier.Apply(report, tableStyle);

                report.Render(false);

                using var stream = new MemoryStream();
                if (asPng) // DIAG : export image pour inspection visuelle.
                {
                    var img = new Stimulsoft.Report.Export.StiImageExportSettings
                    {
                        ImageResolution = 120,
                        PageRange = new Stimulsoft.Report.StiPagesRange(1)
                    };
                    report.ExportDocument(StiExportFormat.ImagePng, stream, img);
                }
                else
                {
                    report.ExportDocument(StiExportFormat.Pdf, stream);
                }

                _logger.LogInformation("Rendu PDF societe={SocieteId} en {Elapsed}ms ({Size} octets)",
                    societeId, stopwatch.ElapsedMilliseconds, stream.Length);

                return stream.ToArray();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>Remplace l'identite societe des donnees d'exemple par la vraie identite (config Axiobat).</summary>
        public static string MergeSociete(string dataJson, string? societeJson)
        {
            if (string.IsNullOrWhiteSpace(societeJson)) return dataJson;
            try
            {
                var root = JsonNode.Parse(dataJson)!.AsObject();
                var over = JsonNode.Parse(societeJson)!.AsObject();
                var societe = root["societe"]?.AsObject() ?? new JsonObject();
                foreach (var kv in over)
                {
                    if (kv.Value is null) continue;
                    var s = kv.Value.ToJsonString();
                    if (s == "\"\"" || s == "null") continue; // ignore vides
                    societe[kv.Key] = JsonNode.Parse(s);
                }
                root["societe"] = societe;
                return root.ToJsonString();
            }
            catch { return dataJson; }
        }

        /// <summary>Injecte le JSON dans le dictionnaire du rapport (dataset "data").</summary>
        public static void RegisterData(StiReport report, string dataJson)
        {
            var dataSet = StiJsonToDataSetConverter.GetDataSet(dataJson);
            dataSet.DataSetName = "data";
            report.RegData("data", dataSet);
            report.Dictionary.Synchronize();
        }

        /// <summary>Fusionne document/societe/options dans un objet JSON unique conforme au dictionnaire des .mrt.</summary>
        public static string BuildDataJson(JsonElement? document, JsonElement? societe, JsonElement? options)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("document");
                WriteElementOrEmpty(writer, document);
                writer.WritePropertyName("societe");
                WriteElementOrEmpty(writer, societe);
                writer.WritePropertyName("options");
                WriteElementOrEmpty(writer, options);
                writer.WriteEndObject();
            }
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteElementOrEmpty(Utf8JsonWriter writer, JsonElement? element)
        {
            if (element.HasValue && element.Value.ValueKind != JsonValueKind.Undefined && element.Value.ValueKind != JsonValueKind.Null)
                element.Value.WriteTo(writer);
            else
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            }
        }
    }
}
