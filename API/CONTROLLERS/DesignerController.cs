using API.CORE.Services;
using API.CORE.Services.Report;
using Microsoft.AspNetCore.Mvc;
using Stimulsoft.Report;
using Stimulsoft.Report.Angular;
using Stimulsoft.Report.Web;
using Stimulsoft.System.Web.UI.WebControls;

namespace API.CONTROLLERS
{
    /// <summary>
    /// Pont du designer web Stimulsoft (composant <c>stimulsoft-designer-angular</c>).
    /// Le composant appelle requestUrl = <c>.../api/designer?templateUrl=…(&amp;access_token=…)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Le modele n'est ni stocke ni resolu ici : l'appelant donne l'URL a laquelle le lire et l'ecrire,
    /// et le moteur s'y adresse en propageant le jeton de l'utilisateur. Les regles d'acces (modele en
    /// lecture seule, droits) appartiennent au magasin, qui refuse la sauvegarde le cas echeant.
    /// </para>
    /// <para>
    /// Le magasin peut joindre au modele les <b>directives de mise en page</b> a lui appliquer : sans
    /// elles, l'utilisateur editerait un modele qui ne ressemble pas a son document. Les <b>images</b>
    /// sont en revanche toujours ecartees ici — ce que le designer affiche est ce qui sera fige dans le
    /// .mrt a l'enregistrement, et un logo en base64 fige rendrait le modele insensible a un changement
    /// d'image.
    /// </para>
    /// </remarks>
    [Produces("application/json")]
    [Route("api/designer")]
    public class DesignerController : Controller
    {
        private readonly TemplateStoreClient _store;

        /// <summary>
        /// Le fichier de traduction de l'interface du designer, ou <c>null</c> pour laisser l'anglais.
        /// </summary>
        /// <remarks>
        /// Le moteur ne sert qu'Axiobat, dont l'interface est en francais : la langue est fixee ici plutot
        /// que negociee, pour que le designer ne parle jamais une autre langue que l'ecran qui l'accueille.
        /// Le repli silencieux couvre le cas ou le fichier n'a pas ete copie : un designer en anglais reste
        /// utilisable, un designer qui refuse de s'ouvrir non.
        /// </remarks>
        private static readonly string? LocalizationFile = ResolveLocalizationFile("fr.xml");

        public DesignerController(TemplateStoreClient store)
        {
            _store = store;
        }

        private static string? ResolveLocalizationFile(string fileName)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Localization", fileName);
            return System.IO.File.Exists(path) ? path : null;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var requestParams = StiAngularDesigner.GetRequestParams(this);
            if (requestParams.Action == StiAction.Undefined)
            {
                var options = new StiAngularDesignerOptions();
                options.Height = Unit.Percentage(100);
                options.Localization = LocalizationFile;

                // Duree de vie de l'edition en cours dans le cache serveur. Les 10 minutes par defaut
                // sont calibrees pour un rapport qu'on ouvre et qu'on enregistre ; mettre en page un
                // modele de devis se fait par allers-retours avec l'apercu, et depasser le delai fait
                // perdre le travail non enregistre.
                options.Server.CacheTimeout = 60;

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
            var templateUrl = TemplateUrl();
            if (templateUrl == null)
                return BadRequest(new { error = "templateUrl est requis." });

            if (!_store.IsAllowed(templateUrl))
                return BadRequest(new { error = "templateUrl n'est pas autorise (TemplateStore:AllowedHosts)." });

            var (mrt, directives) = await _store.GetTemplateAsync(templateUrl, AccessToken());
            if (string.IsNullOrWhiteSpace(mrt))
                return NotFound(new { error = "Modele introuvable ou vide." });

            var report = StiReport.CreateNewReport();
            report.LoadFromString(mrt);

            // Mise en page telle qu'elle sera imprimee, quand le magasin la decrit. Les IMAGES sont
            // volontairement exclues : ce que le designer affiche est ce qui sera fige dans le .mrt a
            // l'enregistrement, et figer un logo ou un papier en-tete en base64 rendrait le modele
            // insensible a un changement d'image.
            if (directives != null)
                DirectiveApplier.Apply(report, directives, includeImages: false);

            return StiAngularDesigner.GetReportResult(this, report);
        }

        private async Task<IActionResult> SaveReportAsync()
        {
            var templateUrl = TemplateUrl();
            if (templateUrl == null)
                return BadRequest(new { error = "templateUrl est requis." });

            if (!_store.IsAllowed(templateUrl))
                return BadRequest(new { error = "templateUrl n'est pas autorise (TemplateStore:AllowedHosts)." });

            var report = StiAngularDesigner.GetReportObject(this);

            var saved = await _store.SaveMrtAsync(templateUrl, report.SaveToString(), AccessToken());
            if (!saved)
                return BadRequest(new { error = "La sauvegarde du modele a ete refusee." });

            return StiAngularDesigner.SaveReportResult(this);
        }

        private string? TemplateUrl()
        {
            var templateUrl = Request.Query["templateUrl"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(templateUrl) ? null : templateUrl;
        }

        /// <summary>
        /// Le jeton de l'utilisateur. Le composant Stimulsoft ne pose pas d'en-tete : il arrive en query
        /// (<c>access_token</c>), avec repli sur <c>Authorization</c> quand l'appel vient d'ailleurs.
        /// </summary>
        private string? AccessToken()
        {
            var fromQuery = Request.Query["access_token"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fromQuery))
                return fromQuery;

            var header = Request.Headers["Authorization"].FirstOrDefault();
            return header?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                ? header.Substring("Bearer ".Length)
                : null;
        }
    }
}
