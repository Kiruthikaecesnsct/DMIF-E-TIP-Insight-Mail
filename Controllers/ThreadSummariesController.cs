using InsightMail.API.Models;
using InsightMail.API.Services;
using InsightMail.Models;
using InsightMail.Repositories;
using InsightMail.Services;
using Microsoft.AspNetCore.Mvc;

namespace InsightMail.API.Controllers
{
    [ApiController]
    [Route("api/v1/threads")]
    public class ThreadSummariesController : ControllerBase
    {
        private readonly IThreadSummaryRepository _repo;
        private readonly IEmailRepository _emailRepo;
        private readonly ThreadSummarizerAgent _summarizer;
        private readonly ILogger<ThreadSummariesController> _logger;

        public ThreadSummariesController(
            IThreadSummaryRepository repo,
            IEmailRepository emailRepo,
            ThreadSummarizerAgent summarizer,
            ILogger<ThreadSummariesController> logger)
        {
            _repo = repo;
            _emailRepo = emailRepo;
            _summarizer = summarizer;
            _logger = logger;
        }

        // POST /api/v1/threads/summarize
        [HttpPost("summarize")]
        public async Task<IActionResult> Summarize([FromBody] SummarizeRequest request)
        {
            var emails = new List<Email>();
            foreach (var id in request.EmailIds)
            {
                var email = await _emailRepo.GetByIdAsync(id);
                if (email == null)
                    return NotFound($"Email {id} not found");
                emails.Add(email);
            }

            var summary = await _summarizer.SummarizeThreadAsync(emails);
            summary.ThreadId = request.ThreadId ?? emails.First().Subject;

            var saved = await _repo.CreateAsync(summary);
            return CreatedAtAction(nameof(GetById), new { id = saved.Id }, saved);
        }

        // POST /api/v1/threads/summarize/background
        [HttpPost("summarize/background")]
        public IActionResult SummarizeBackground([FromBody] SummarizeRequest request)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var emails = new List<Email>();
                    foreach (var id in request.EmailIds)
                    {
                        var email = await _emailRepo.GetByIdAsync(id);
                        if (email != null) emails.Add(email);
                    }

                    if (!emails.Any()) return;

                    var summary = await _summarizer.SummarizeThreadAsync(emails);
                    summary.ThreadId = request.ThreadId ?? emails.First().Subject;
                    await _repo.CreateAsync(summary);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background summarization failed for thread {ThreadId}",
                        request.ThreadId);
                }
            });

            return Accepted(new { message = "Summarization started", threadId = request.ThreadId });
        }

        // GET /api/v1/threads/summaries/{id}
        [HttpGet("summaries/{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var summary = await _repo.GetByIdAsync(id);
            return summary == null ? NotFound() : Ok(summary);
        }

        // GET /api/v1/threads/summaries/thread/{threadId}
        [HttpGet("summaries/thread/{threadId}")]
        public async Task<IActionResult> GetByThreadId(string threadId)
        {
            var summary = await _repo.GetByThreadIdAsync(threadId);
            return summary == null ? NotFound() : Ok(summary);
        }

        // DELETE /api/v1/threads/summaries/{id}
        [HttpDelete("summaries/{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return NotFound();
            await _repo.DeleteAsync(id);
            return NoContent();
        }
    }

    public class SummarizeRequest
    {
        public List<string> EmailIds { get; set; } = new();
        public string? ThreadId { get; set; }
    }
}