using API.CORE.Middlewares;
using API.CORE.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.CONTROLLERS
{
    /// <summary>
    /// Assets editique de la societe (logo, papier entete, cachet) pousses par le webadmin depuis
    /// la vraie config Axiobat, puis injectes dans les modeles au rendu et dans le designer.
    /// </summary>
    [ApiController]
    [Route("api/societe/assets")]
    public class SocieteAssetsController : ControllerBase
    {
        private readonly SocieteAssetsService _assets;
        private readonly TenantContext _tenant;

        public SocieteAssetsController(SocieteAssetsService assets, TenantContext tenant)
        {
            _assets = assets;
            _tenant = tenant;
        }

        public class SetAssetsRequest
        {
            public string? Logo { get; set; }
            public string? Background { get; set; }
            public string? Cachet { get; set; }
            public string? MainColor { get; set; }
        }

        [HttpPut]
        public IActionResult Set([FromBody] SetAssetsRequest request)
        {
            if (string.IsNullOrWhiteSpace(_tenant.SocieteId))
                return BadRequest(new { error = "Societe non resolue." });

            // Fusion PARTIELLE : null/absent = conserver l'existant ; "" = effacer ; valeur = definir.
            // Sinon les pushs logo/couleur (qui envoient background:null) effaceraient le papier entete
            // deja pousse par l'utilisateur (le store etant en memoire, tout objet remplace ecrase le fond).
            var existing = _assets.Get(_tenant.SocieteId) ?? new SocieteAssets();
            _assets.Set(_tenant.SocieteId, new SocieteAssets
            {
                Logo = Merge(existing.Logo, request.Logo),
                Background = Merge(existing.Background, request.Background),
                Cachet = Merge(existing.Cachet, request.Cachet),
                MainColor = Merge(existing.MainColor, request.MainColor)
            });
            return Ok();

            static string? Merge(string? current, string? incoming)
                => incoming is null ? current : (incoming.Length == 0 ? null : incoming);
        }

        [HttpGet]
        public IActionResult Get()
        {
            var a = _assets.Get(_tenant.SocieteId);
            // Renvoie les assets reels (logo/cachet/fond/couleur) pour que le webadmin puisse les afficher
            // dans l'ecran de config meme quand l'API Axiobat (token strict) n'est pas joignable.
            return Ok(new
            {
                hasLogo = !string.IsNullOrEmpty(a?.Logo),
                hasBackground = !string.IsNullOrEmpty(a?.Background),
                logo = a?.Logo,
                cachet = a?.Cachet,
                background = a?.Background,
                mainColor = a?.MainColor
            });
        }
    }
}
