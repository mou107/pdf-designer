using API.CORE.Schemas;
using API.CORE.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.CONTROLLERS
{
    /// <summary>
    /// Le seul endpoint metier du moteur : un modele, des donnees, des directives -> un document.
    /// </summary>
    /// <remarks>
    /// Rien n'est resolu ici. L'appelant fournit le <c>.mrt</c>, ses donnees et la liste de ce qu'il veut
    /// appliquer dessus ; le moteur ne connait ni modele stocke, ni societe, ni type de document, ni
    /// aucun nom de champ. C'est ce qui le rend utilisable par n'importe quel projet.
    /// </remarks>
    [ApiController]
    [Route("api/render")]
    public class RenderController : ControllerBase
    {
        private readonly RenderService _render;

        public RenderController(RenderService render)
        {
            _render = render;
        }

        /// <summary>
        /// Rend un document.
        /// </summary>
        /// <param name="request">le modele, les donnees et les directives</param>
        /// <param name="format">
        /// <c>pdf</c> (defaut) ou <c>png</c>. L'export image ne rend que la premiere page et ignore les
        /// documents joints : il sert a inspecter visuellement un rendu.
        /// </param>
        [HttpPost]
        [Produces("application/pdf", "image/png")]
        public async Task<IActionResult> Render([FromBody] RenderRequest request, [FromQuery] string? format = null)
        {
            if (string.IsNullOrWhiteSpace(request?.Mrt))
                return BadRequest(new { error = "mrt est requis : le moteur ne detient aucun modele." });

            var png = string.Equals(format, "png", StringComparison.OrdinalIgnoreCase);
            var output = await _render.RenderAsync(request,
                png ? RenderService.OutputFormat.Png : RenderService.OutputFormat.Pdf);

            var fileName = string.IsNullOrWhiteSpace(request.FileName) ? "document" : request.FileName;
            return png
                ? File(output, "image/png", $"{fileName}.png")
                : File(output, "application/pdf", $"{fileName}.pdf");
        }
    }
}
