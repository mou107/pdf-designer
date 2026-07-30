using System.Diagnostics;
using System.Text.Json;
using API.CORE.Schemas;
using API.CORE.Services.Report;
using Stimulsoft.Base;
using Stimulsoft.Report;

namespace API.CORE.Services
{
    /// <summary>
    /// Le moteur : un modele .mrt, des donnees, des directives -> un document. Sans etat, sans base,
    /// sans disque et sans metier — tout arrive dans la requete.
    /// </summary>
    /// <remarks>
    /// Limite de concurrence et delai d'attente configurables
    /// (<c>Render:MaxConcurrency</c> / <c>Render:TimeoutSeconds</c>) : un rendu Stimulsoft est couteux en
    /// memoire, les laisser tous partir en meme temps ferait tomber le service.
    /// </remarks>
    public class RenderService
    {
        private static SemaphoreSlim? _semaphore;
        private readonly ILogger<RenderService> _logger;
        private readonly int _timeoutSeconds;

        public RenderService(IConfiguration configuration, ILogger<RenderService> logger)
        {
            _logger = logger;
            _timeoutSeconds = configuration.GetValue("Render:TimeoutSeconds", 60);
            _semaphore ??= new SemaphoreSlim(Math.Max(1, configuration.GetValue("Render:MaxConcurrency", 4)));
        }

        /// <summary>Format de sortie demande.</summary>
        public enum OutputFormat
        {
            Pdf,
            Png
        }

        /// <summary>
        /// Rend la requete et renvoie le document produit.
        /// </summary>
        /// <remarks>
        /// L'application des directives est deleguee a <see cref="DirectiveApplier"/>, partage avec le
        /// pont du designer : ce que l'utilisateur edite ressemble ainsi a ce qui sera imprime.
        /// </remarks>
        public async Task<byte[]> RenderAsync(RenderRequest request, OutputFormat format = OutputFormat.Pdf)
        {
            if (string.IsNullOrWhiteSpace(request.Mrt))
                throw new ArgumentException("Le contenu du modele (mrt) est requis.", nameof(request));

            await _semaphore!.WaitAsync(TimeSpan.FromSeconds(_timeoutSeconds));
            try
            {
                var stopwatch = Stopwatch.StartNew();

                var report = StiReport.CreateNewReport();
                report.LoadFromString(request.Mrt);

                RegisterData(report, request.Data, request.DataSetName);
                DirectiveApplier.Apply(report, request, includeImages: true);

                report.Render(false);

                using var stream = new MemoryStream();
                if (format == OutputFormat.Png)
                {
                    // Export image : inspection visuelle d'un rendu, limitee a la premiere page.
                    // StiPngExportSettings et non StiImageExportSettings : ce dernier produit du JPEG,
                    // ce qui contredirait le Content-Type annonce.
                    report.ExportDocument(StiExportFormat.ImagePng, stream,
                        new Stimulsoft.Report.Export.StiPngExportSettings
                        {
                            ImageResolution = 120,
                            PageRange = new StiPagesRange(1)
                        });
                }
                else
                {
                    report.ExportDocument(StiExportFormat.Pdf, stream);
                }

                _logger.LogInformation("Rendu {Format} en {Elapsed}ms ({Size} octets)",
                    format, stopwatch.ElapsedMilliseconds, stream.Length);

                var output = stream.ToArray();

                // Les documents joints sont concatenes au PDF produit. Sans objet en export image, qui ne
                // rend que la premiere page.
                return format == OutputFormat.Png
                    ? output
                    : AttachmentsMerger.Append(output, request.Attachments, _logger);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Enregistre les donnees de la requete comme source du rapport, telles quelles.
        /// </summary>
        /// <remarks>
        /// Aucune forme n'est imposee : le moteur ne lit pas ces donnees, il les met a disposition des
        /// expressions du modele. C'est ce qui lui permet de servir des applications aux modeles de
        /// donnees totalement differents.
        /// </remarks>
        private static void RegisterData(StiReport report, JsonElement? data, string? dataSetName)
        {
            var name = string.IsNullOrWhiteSpace(dataSetName) ? "data" : dataSetName;
            var json = data.HasValue && data.Value.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null)
                ? data.Value.GetRawText()
                : "{}";

            var dataSet = StiJsonToDataSetConverter.GetDataSet(json);
            dataSet.DataSetName = name;
            report.RegData(name, dataSet);
            report.Dictionary.Synchronize();
        }
    }
}
