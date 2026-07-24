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

        public RenderController(TemplateService templates, RenderService render, ILogger<RenderController> logger)
        {
            _templates = templates;
            _render = render;
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
            /// <summary>Donnees du document (JSON { document, societe, options }) fournies par l'appelant (Axiobat)
            /// pour l'apercu. A defaut, un squelette generique en code est utilise (SampleSkeleton).</summary>
            public string? DataJson { get; set; }
            /// <summary>Config simple courante (couleurs/colonnes) pour l'apercu live pendant l'edition — optionnel.</summary>
            public string? ConfigJson { get; set; }
            /// <summary>Numero de modele a previsualiser (1-7) — bascule de modele dans l'editeur ; a defaut celui du template.</summary>
            public int? Model { get; set; }
            /// <summary>Identite reelle de la societe (nom, adresse, siret, mentions…) pour remplacer les donnees d'exemple.</summary>
            public string? SocieteJson { get; set; }
            /// <summary>Style de tableau (1-3) — bordures / lignes alternees.</summary>
            public int? TableStyle { get; set; }
            /// <summary>Dimensions du logo / cachet en pixels — redimensionnent leur boite au rendu (0/absent = defaut).</summary>
            public double? LogoWidth { get; set; }
            public double? LogoHeight { get; set; }
            public double? CachetWidth { get; set; }
            public double? CachetHeight { get; set; }
            /// <summary>Logo / cachet (base64) de la societe CONNECTEE. Quand OverrideAssets=true, ils remplacent
            /// l'asset en cache pour l'apercu (null = pas de logo) — garantit que l'apercu reflete la societe courante.</summary>
            public bool OverrideAssets { get; set; }
            public string? Logo { get; set; }
            public string? Cachet { get; set; }
            /// <summary>Papier entete (fond, base64) + couleur principale de la societe CONNECTEE — appliques en
            /// override pour un apercu sans etat (plus de dependance au cache memoire du microservice).</summary>
            public string? Background { get; set; }
            public string? MainColor { get; set; }
        }

        /// <summary>Preversion d'un modele avec les donnees exemples du docType + la config societe (couleurs, logo…).</summary>
        private readonly ILogger<RenderController> _logger;

        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromQuery] string templateId, [FromQuery] bool png = false, [FromBody] PreviewRequest? request = null)
        {
            var template = await _templates.FindReadableAsync(templateId);
            if (template == null) return NotFound();

            // Trace de diagnostic : que recoit-on reellement du webadmin ?
            _logger.LogInformation("PREVIEW model={Model} tableStyle={Table} configLen={Len} societeLen={SocLen} DIAG-SOCIETE={Soc}",
                request?.Model, request?.TableStyle, request?.ConfigJson?.Length ?? 0,
                request?.SocieteJson?.Length ?? 0,
                request?.SocieteJson?.Length > 600 ? request.SocieteJson.Substring(0, 600) : request?.SocieteJson);

            // Donnees d'apercu : fournies par l'appelant (Axiobat) ; a defaut, squelette generique en code.
            var sampleJson = string.IsNullOrWhiteSpace(request?.DataJson)
                ? SampleSkeleton.GetJson(template.DocType)
                : request!.DataJson;
            // Remplace l'identite societe des donnees par la vraie (config Axiobat) si fournie.
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
            var pdf = await _render.RenderFileAsync(mrtPath, template.SocieteId, sampleJson, request?.ConfigJson, tableStyle,
                request?.LogoWidth, request?.LogoHeight, request?.CachetWidth, request?.CachetHeight,
                request?.OverrideAssets == true, request?.Logo, request?.Cachet, png,
                backgroundOverride: request?.Background, mainColorOverride: request?.MainColor);
            return File(pdf, png ? "image/png" : "application/pdf", png ? $"preview-{template.DocType}.png" : $"preview-{template.DocType}.pdf");
        }
    }
}
