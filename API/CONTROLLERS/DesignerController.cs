using API.CORE.Services;
using Microsoft.AspNetCore.Mvc;
using Stimulsoft.Report;
using Stimulsoft.Report.Angular;
using Stimulsoft.Report.Web;
using Stimulsoft.System.Web.UI.WebControls;

namespace API.CONTROLLERS
{
    /// <summary>
    /// Pont du designer web Stimulsoft (composant stimulsoft-designer-angular).
    /// Le composant appelle requestUrl = .../api/designer?templateId=xxx(&access_token=yyy).
    /// </summary>
    [Produces("application/json")]
    [Route("api/designer")]
    public class DesignerController : Controller
    {
        private readonly TemplateService _templates;
        private readonly TemplateStorageService _storage;
        private readonly SampleDataService _sampleData;

        public DesignerController(TemplateService templates, TemplateStorageService storage, SampleDataService sampleData)
        {
            _templates = templates;
            _storage = storage;
            _sampleData = sampleData;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var requestParams = StiAngularDesigner.GetRequestParams(this);
            if (requestParams.Action == StiAction.Undefined)
            {
                var options = new StiAngularDesignerOptions();
                options.Height = Unit.Percentage(100);
                return StiAngularDesigner.DesignerDataResult(requestParams, options);
            }

            return StiAngularDesigner.ProcessRequestResult(this);
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            var requestParams = StiAngularDesigner.GetRequestParams(this);
            if (requestParams.ComponentType == StiComponentType.Designer)
            {
                switch (requestParams.Action)
                {
                    case StiAction.GetReport:
                        return await GetReportAsync();

                    case StiAction.SaveReport:
                        return await SaveReportAsync();
                }
            }

            return StiAngularDesigner.ProcessRequestResult(this);
        }

        private async Task<IActionResult> GetReportAsync()
        {
            var template = await ResolveTemplateAsync();
            if (template == null)
                return NotFound(new { error = "Modele introuvable (templateId manquant ou inconnu)." });

            var report = StiReport.CreateNewReport();
            report.Load(_storage.GetAbsolutePath(template.FilePath));
            report.ReportName = template.Name;

            // Dictionnaire synchronise sur les donnees exemples du docType : l'utilisateur
            // voit les champs disponibles et la preview du designer rend avec ces donnees.
            RenderService.RegisterData(report, await _sampleData.GetSampleDataAsync(template.DocType));

            return StiAngularDesigner.GetReportResult(this, report);
        }

        private async Task<IActionResult> SaveReportAsync()
        {
            var template = await ResolveTemplateAsync();
            if (template == null)
                return NotFound(new { error = "Modele introuvable." });

            if (template.SocieteId == null)
                return BadRequest(new { error = "Les modeles standards sont en lecture seule : dupliquez-les d'abord." });

            var report = StiAngularDesigner.GetReportObject(this);
            await _templates.SaveNewVersionAsync(template, report.SaveToString());

            return StiAngularDesigner.SaveReportResult(this);
        }

        private async Task<DATABASE.Entities.ReportTemplate?> ResolveTemplateAsync()
        {
            var templateId = Request.Query["templateId"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(templateId) ? null : await _templates.FindReadableAsync(templateId);
        }
    }
}
