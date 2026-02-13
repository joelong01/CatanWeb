using Microsoft.AspNetCore.Mvc;
using Catan3.Shared.Models;
using Catan3.GameService.Services;

namespace Catan3.GameService.Controllers;

[ApiController]
[Route("api/game")]
public class TemplateApiController : ControllerBase
{
    private readonly GameTemplateService _templateService;

    public TemplateApiController(GameTemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpGet("templates")]
    public async Task<IActionResult> ListTemplates([FromQuery] string? category)
    {
        var templates = await _templateService.ListAsync(category);
        return Ok(templates);
    }

    [HttpGet("templates/{id}")]
    public async Task<IActionResult> GetTemplate(string id)
    {
        var template = await _templateService.GetAsync(id);
        if (template == null)
        {
            return NotFound(new { error = $"Template '{id}' not found." });
        }

        return Ok(template);
    }

    [HttpPut("templates/{id}")]
    public async Task<IActionResult> UpdateTemplate(string id, [FromBody] GameTemplateData template)
    {
        template.Id = id;
        try
        {
            await _templateService.SaveAsync(template);
            return Ok(template);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Template '{id}' not found." });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate([FromBody] GameTemplateData template)
    {
        try
        {
            var created = await _templateService.SaveAsAsync(template, template.Id, template.Name);
            return CreatedAtAction(nameof(GetTemplate), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpDelete("templates/{id}")]
    public async Task<IActionResult> DeleteTemplate(string id)
    {
        try
        {
            await _templateService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Template '{id}' not found." });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }
}
