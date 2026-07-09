using API.CORE.Schemas;
using API.CORE.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.CONTROLLERS
{
    [ApiController]
    [Route("api/templates")]
    public class TemplatesController : ControllerBase
    {
        private readonly TemplateService _templates;
        private readonly SeedService _seeds;

        public TemplatesController(TemplateService templates, SeedService seeds)
        {
            _templates = templates;
            _seeds = seeds;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? docType)
        {
            if (docType != null && !DocTypes.IsValid(docType))
                return BadRequest(new { error = $"docType invalide. Valeurs : {string.Join(", ", DocTypes.All)}" });

            return Ok(await _templates.ListAsync(docType));
        }

        [HttpGet("doc-types")]
        public IActionResult GetDocTypes() => Ok(DocTypes.All);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTemplateRequest request)
        {
            if (!DocTypes.IsValid(request.DocType))
                return BadRequest(new { error = $"docType invalide. Valeurs : {string.Join(", ", DocTypes.All)}" });

            var seedContent = request.From == "blank"
                ? await _seeds.BuildBlankMrtAsync(request.DocType)
                : await _seeds.GetSeedMrtContentAsync(request.DocType, request.Model);

            var created = await _templates.CreateAsync(request, seedContent);
            return Ok(created);
        }

        [HttpPost("{id}/duplicate")]
        public async Task<IActionResult> Duplicate(string id)
        {
            var source = await _templates.FindReadableAsync(id);
            if (source == null) return NotFound();
            return Ok(await _templates.DuplicateAsync(source));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateTemplateRequest request)
        {
            var template = await _templates.FindOwnedAsync(id);
            if (template == null) return NotFound();
            return Ok(await _templates.UpdateAsync(template, request));
        }

        [HttpPut("{id}/config")]
        public async Task<IActionResult> SaveConfig(string id, [FromBody] SaveConfigRequest request)
        {
            var template = await _templates.FindOwnedAsync(id);
            if (template == null) return NotFound();
            return Ok(await _templates.SaveConfigAsync(template, request));
        }

        [HttpPost("{id}/apply-to-all")]
        public async Task<IActionResult> ApplyToAll(string id)
        {
            var template = await _templates.FindOwnedAsync(id);
            if (template == null) return NotFound();
            var (applied, skipped) = await _templates.ApplyToAllDocTypesAsync(template);
            return Ok(new { applied, skipped });
        }

        [HttpPut("{id}/default")]
        public async Task<IActionResult> SetDefault(string id)
        {
            var template = await _templates.FindOwnedAsync(id);
            if (template == null) return NotFound();
            await _templates.SetDefaultAsync(template);
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var template = await _templates.FindOwnedAsync(id);
            if (template == null) return NotFound();

            try
            {
                await _templates.SoftDeleteAsync(template);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            return Ok();
        }

        [HttpPost("{id}/restore/{version:int}")]
        public async Task<IActionResult> Restore(string id, int version)
        {
            var template = await _templates.FindOwnedAsync(id);
            if (template == null) return NotFound();

            try
            {
                await _templates.RestoreVersionAsync(template, version);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            return Ok();
        }
    }
}
