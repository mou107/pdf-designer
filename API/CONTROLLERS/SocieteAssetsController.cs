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

            _assets.Set(_tenant.SocieteId, new SocieteAssets
            {
                Logo = request.Logo,
                Background = request.Background,
                Cachet = request.Cachet,
                MainColor = request.MainColor
            });
            return Ok();
        }

        [HttpGet]
        public IActionResult Get()
        {
            var a = _assets.Get(_tenant.SocieteId);
            return Ok(new { hasLogo = !string.IsNullOrEmpty(a?.Logo), hasBackground = !string.IsNullOrEmpty(a?.Background) });
        }
    }
}
