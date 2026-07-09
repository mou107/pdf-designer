using API.CORE.Schemas;
using API.CORE.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.CONTROLLERS
{
    [ApiController]
    [Route("api/render")]
    public class RenderController : ControllerBase
    {
        private readonly TemplateService _templates;
        private readonly RenderService _render;
        private readonly SampleDataService _sampleData;

        public RenderController(TemplateService templates, RenderService render, SampleDataService sampleData, ILogger<RenderController> logger)
        {
            _templates = templates;
            _render = render;
            _sampleData = sampleData;
            _logger = logger;
        }

        /// <summary>
        /// Rendu d'un document : PdfRenderPayload (JSON complet envoye par le client web/mobile) -> PDF.
        /// Resolution du template : templateId explicite -> defaut (societe, docType) -> seed global.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Render([FromBody] PdfRenderPayload payload)
        {
            if (!DocTypes.IsValid(payload.DocType))
                return BadRequest(new { error = $"docType invalide. Valeurs : {string.Join(", ", DocTypes.All)}" });

            var template = await _templates.ResolveForRenderAsync(payload.DocType, payload.TemplateId);
            if (template == null)
                return NotFound(new { error = $"Aucun modele disponible pour {payload.DocType}." });

            var pdf = await _render.RenderPdfAsync(template, payload);
            return File(pdf, "application/pdf", $"{payload.DocType}.pdf");
        }

        public class PreviewRequest
        {
            /// <summary>Config simple courante (couleurs/colonnes) pour l'apercu live pendant l'edition — optionnel.</summary>
            public string? ConfigJson { get; set; }
            /// <summary>Numero de modele a previsualiser (1-7) — bascule de modele dans l'editeur ; a defaut celui du template.</summary>
            public int? Model { get; set; }
            /// <summary>Identite reelle de la societe (nom, adresse, siret, mentions…) pour remplacer les donnees d'exemple.</summary>
            public string? SocieteJson { get; set; }
            /// <summary>Style de tableau (1-3) — bordures / lignes alternees.</summary>
            public int? TableStyle { get; set; }
        }

        /// <summary>Preversion d'un modele avec les donnees exemples du docType + la config societe (couleurs, logo…).</summary>
        private readonly ILogger<RenderController> _logger;

        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromQuery] string templateId, [FromBody] PreviewRequest? request = null)
        {
            var template = await _templates.FindReadableAsync(templateId);
            if (template == null) return NotFound();

            // Trace de diagnostic : que recoit-on reellement du webadmin ?
            _logger.LogInformation("PREVIEW model={Model} tableStyle={Table} configLen={Len} config={Config} societeLen={SocLen}",
                request?.Model, request?.TableStyle, request?.ConfigJson?.Length ?? 0,
                request?.ConfigJson?.Length > 400 ? request.ConfigJson.Substring(0, 400) : request?.ConfigJson,
                request?.SocieteJson?.Length ?? 0);

            var sampleJson = await _sampleData.GetSampleDataAsync(template.DocType);
            // Remplace l'identite societe d'exemple par la vraie (config Axiobat) si fournie.
            sampleJson = RenderService.MergeSociete(sampleJson, request?.SocieteJson);

            // Bascule de modele dans l'editeur : on rend le seed du modele choisi (mise en page 1-7),
            // avec le logo + la couleur de la societe du template.
            string mrtPath = _templates.GetAbsolutePath(template);
            if (request?.Model is int m && m != template.Model)
            {
                var seed = await _templates.FindSeedAsync(template.DocType, m);
                if (seed != null) mrtPath = _templates.GetAbsolutePath(seed);
            }

            var tableStyle = request?.TableStyle ?? template.TableStyle;
            var pdf = await _render.RenderFileAsync(mrtPath, template.SocieteId, sampleJson, request?.ConfigJson, tableStyle);
            return File(pdf, "application/pdf", $"preview-{template.DocType}.pdf");
        }
    }
}
