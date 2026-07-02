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

        public RenderController(TemplateService templates, RenderService render, SampleDataService sampleData)
        {
            _templates = templates;
            _render = render;
            _sampleData = sampleData;
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

        /// <summary>Preversion d'un modele avec les donnees exemples du docType.</summary>
        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromQuery] string templateId)
        {
            var template = await _templates.FindReadableAsync(templateId);
            if (template == null) return NotFound();

            var sampleJson = await _sampleData.GetSampleDataAsync(template.DocType);
            var pdf = await _render.RenderPdfAsync(template, sampleJson);
            return File(pdf, "application/pdf", $"preview-{template.DocType}.pdf");
        }
    }
}
