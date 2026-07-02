using System.Diagnostics;
using System.Text.Json;
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
        private readonly ILogger<RenderService> _logger;
        private readonly int _timeoutSeconds;

        public RenderService(TemplateStorageService storage, IConfiguration configuration, ILogger<RenderService> logger)
        {
            _storage = storage;
            _logger = logger;
            _timeoutSeconds = configuration.GetValue("Render:TimeoutSeconds", 60);
            _semaphore ??= new SemaphoreSlim(Math.Max(1, configuration.GetValue("Render:MaxConcurrency", 4)));
        }

        public async Task<byte[]> RenderPdfAsync(ReportTemplate template, PdfRenderPayload payload)
        {
            var json = BuildDataJson(payload.Document, payload.Societe, payload.Options);
            return await RenderPdfAsync(template, json);
        }

        public async Task<byte[]> RenderPdfAsync(ReportTemplate template, string dataJson)
        {
            await _semaphore!.WaitAsync(TimeSpan.FromSeconds(_timeoutSeconds));
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var report = StiReport.CreateNewReport();
                report.Load(_storage.GetAbsolutePath(template.FilePath));

                RegisterData(report, dataJson);

                report.Render(false);

                using var stream = new MemoryStream();
                report.ExportDocument(StiExportFormat.Pdf, stream);

                _logger.LogInformation(
                    "Rendu PDF {DocType} template={TemplateId} v{Version} societe={SocieteId} en {Elapsed}ms ({Size} octets)",
                    template.DocType, template.Id, template.Version, template.SocieteId, stopwatch.ElapsedMilliseconds, stream.Length);

                return stream.ToArray();
            }
            finally
            {
                _semaphore.Release();
            }
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
