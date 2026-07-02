using API.CORE.Schemas;
using API.CORE.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.CONTROLLERS
{
    /// <summary>
    /// Schemas JSON + donnees exemples par type de document — source de verite
    /// du dictionnaire des .mrt et des mappers clients (web/mobile).
    /// </summary>
    [ApiController]
    [Route("api/schemas")]
    public class SchemasController : ControllerBase
    {
        private readonly SampleDataService _sampleData;

        public SchemasController(SampleDataService sampleData)
        {
            _sampleData = sampleData;
        }

        [HttpGet("{docType}")]
        public async Task<IActionResult> Get(string docType)
        {
            if (!DocTypes.IsValid(docType))
                return BadRequest(new { error = $"docType invalide. Valeurs : {string.Join(", ", DocTypes.All)}" });

            var schema = await _sampleData.GetSchemaAsync(docType);
            var sample = await _sampleData.GetSampleDataAsync(docType);

            return Content($"{{\"schema\":{schema ?? "null"},\"sample\":{sample}}}", "application/json");
        }
    }
}
