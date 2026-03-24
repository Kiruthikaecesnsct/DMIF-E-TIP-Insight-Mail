using InsightMail.Models;
using InsightMail.Services;
using Microsoft.AspNetCore.Mvc;

namespace InsightMail.API.Controllers
{
    [ApiController]
    [Route("api/v1/draft")]
    public class DraftController : ControllerBase
    {
        private readonly IDraftAssistantAgent _draftAgent;
        private readonly EmailTemplateService _templateService;

        public DraftController(
            IDraftAssistantAgent draftAgent,
            EmailTemplateService templateService)
        {
            _draftAgent = draftAgent;
            _templateService = templateService;
        }

        // POST /api/v1/draft/compose
        [HttpPost("compose")]
        public async Task<IActionResult> Compose([FromBody] ComposeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Purpose))
                return BadRequest("Purpose is required.");

            var draft = await _draftAgent.ComposeEmailAsync(request);
            return Ok(draft);
        }

        // GET /api/v1/draft/templates
        [HttpGet("templates")]
        public IActionResult GetTemplates()
        {
            var templates = _templateService.GetAllTemplates()
                .Select(kvp => new
                {
                    key = kvp.Key,
                    name = kvp.Value.Name,
                    requiredFields = kvp.Value.RequiredFields
                });
            return Ok(templates);
        }

        // POST /api/v1/draft/templates/{key}
        [HttpPost("templates/{key}")]
        public IActionResult ApplyTemplate(string key,
            [FromBody] Dictionary<string, string> fields)
        {
            try
            {
                var draft = _templateService.ApplyTemplate(key, fields);
                return Ok(draft);
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Template '{key}' not found.");
            }
        }
    }
}

