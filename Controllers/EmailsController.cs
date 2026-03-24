//using InsightMail.API.Models;
//using InsightMail.API.Services;
//using Microsoft.AspNetCore.Mvc;

//namespace InsightMail.API.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class EmailsController : ControllerBase
//    {
//        // GET: api/emails 
//        [HttpGet]
//        public ActionResult<List<Email>> GetEmails()
//        {
//            var emails = new List<Email>
//    {
//        new Email
//        {
//            Sender = "john@example.com",
//            Subject = "Project Update",
//            Body = "Here's the latest on the project...",
//            ReceivedDate = DateTime.UtcNow.AddHours(-2)
//        }
//    };

//            return Ok(emails);
//        }



//        // GET: api/emails/{id} 
//        [HttpGet("{id}")]
//        public ActionResult<Email> GetEmail(string id)
//        {
//            // For now, return sample 
//            var email = new Email
//            {
//                Id = id,
//                Sender = "sarah@example.com",
//                Subject = "Budget Approval",
//                Body = "The budget has been approved...",
//                ReceivedDate = DateTime.UtcNow.AddDays(-1)
//            };

//            return Ok(email);
//        }


//        // POST: api/emails 
//        [HttpPost]
//        public ActionResult<string> UploadEmail()
//        {
//            return Ok("Email uploaded successfully");
//        }
//    }
//}




//PART 2 FILE UPLOAD
//using Microsoft.AspNetCore.Mvc;
//using InsightMail.API.Services;
//using InsightMail.API.Models;

//namespace InsightMail.API.Controllers
//{
//    [ApiController]
//    [Route("api/v1/[controller]")]
//    public class EmailsController : ControllerBase
//    {
//        private readonly IEmailParserService _parser;
//        private static List<Email> _emails = new(); // Temporary storage 

//        public EmailsController(IEmailParserService parser)
//        {
//            _parser = parser;
//        }

//        [HttpPost("upload")]
//        public async Task<ActionResult<Email>> UploadEmail(
//            IFormFile file)
//        {
//            // Validate file 
//            if (file == null || file.Length == 0)
//            {
//                return BadRequest("No file uploaded");
//            }

//            if (!file.FileName.EndsWith(".eml"))
//            {
//                return BadRequest("Only .eml files are supported");
//            }

//            try
//            {
//                // Parse email 
//                using var stream = file.OpenReadStream();
//                var email = await _parser.ParseEmailAsync(stream);

//                // Store (temporary - will use MongoDB next) 
//                _emails.Add(email);

//                return CreatedAtAction(
//                    nameof(GetEmail),
//                    new { id = email.Id },
//                    email);
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500,
//                    $"Error parsing email: {ex.Message}");
//            }
//        }
//        [HttpPost("upload/batch")]
//        public async Task<ActionResult<List<Email>>> UploadMultipleEmails(
//    List<IFormFile> files)
//        {
//            if (files == null || !files.Any())
//            {
//                return BadRequest("No files uploaded");
//            }

//            var uploadedEmails = new List<Email>();
//            var errors = new List<string>();

//            foreach (var file in files)
//            {
//                if (!file.FileName.EndsWith(".eml"))
//                {
//                    errors.Add($"{file.FileName}: Not a .eml file");
//                    continue;
//                }

//                try
//                {
//                    using var stream = file.OpenReadStream();
//                    var email = await _parser.ParseEmailAsync(stream);
//                    _emails.Add(email);
//                    uploadedEmails.Add(email);
//                }
//                catch (Exception ex)
//                {
//                    errors.Add($"{file.FileName}: {ex.Message}");
//                }
//            }

//            var result = new
//            {
//                Uploaded = uploadedEmails.Count,
//                Failed = errors.Count,
//                Emails = uploadedEmails,
//                Errors = errors
//            };

//            return Ok(result);
//        }

//        [HttpGet]
//        public ActionResult<List<Email>> GetEmails()
//        {
//            return Ok(_emails);
//        }

//        [HttpGet("{id}")]
//        public ActionResult<Email> GetEmail(string id)
//        {
//            var email = _emails.FirstOrDefault(e => e.Id == id);

//            if (email == null)
//                return NotFound();

//            return Ok(email);
//        }
//    }
//}



//PART 3:USING EMAIL REPOSITORY
using InsightMail.API.Hubs;
using InsightMail.API.Models;
using InsightMail.API.Services;
using InsightMail.Models;
using InsightMail.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace InsightMail.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class EmailsController : ControllerBase
    {
        private readonly IEmailParserService _parser;
        private readonly IEmailRepository _repository;
        private readonly IClassifierService _classifier;
        private readonly IActionExtractorService _extractor;
        private readonly IActionItemRepository _actionItemRepository;
        private readonly IEmbeddingService _embeddingService;
        private readonly IHubContext<EmailHub> _hubContext;
        private readonly ILogger<EmailsController> _logger;

        public EmailsController(
            IEmailParserService parser,
            IEmailRepository repository,
            IClassifierService classifier,
            IActionExtractorService extractor,
            IActionItemRepository actionItemRepository,
            IEmbeddingService embeddingService,
            IHubContext<EmailHub> hubContext,
            ILogger<EmailsController> logger)
        {
            _parser = parser;
            _repository = repository;
            _classifier = classifier;
            _extractor = extractor;
            _actionItemRepository = actionItemRepository;
            _embeddingService = embeddingService;
            _hubContext = hubContext;
            _logger = logger;
        }

        // ── Upload ────────────────────────────────────────────────────────

        [HttpPost("upload")]
        public async Task<ActionResult<Email>> UploadEmail(
            IFormFile file,
            [FromServices] EmailThreadSplitter threadSplitter)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            if (!file.FileName.EndsWith(".eml"))
                return BadRequest("Only .eml files are supported");

            try
            {
                using var stream = file.OpenReadStream();
                var email = await _parser.ParseEmailAsync(stream);

                var emails = threadSplitter.SplitThread(email);
                _logger.LogInformation("Email split into {Count} messages", emails.Count);

                foreach (var e in emails)
                    await _repository.CreateAsync(e);

                var topEmail = emails.Last();

                // Fire background AI processing with SignalR updates
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Notify("Classifier", "Processing",
                            $"Classifying: {topEmail.Subject}");

                        var classificationTask = _classifier.ClassifyEmailAsync(topEmail);

                        await Notify("ActionExtractor", "Processing",
                            "Extracting action items...");

                        var actionTask = _extractor.ExtractActionItemsAsync(topEmail);

                        await Notify("EmbeddingService", "Processing",
                            "Generating semantic embedding...");

                        var embeddingTask = _embeddingService.GenerateEmbeddingAsync(
                            $"{topEmail.Subject} {topEmail.Body}");

                        await Task.WhenAll(classificationTask, actionTask, embeddingTask);

                        var classification = await classificationTask;
                        var actionItems = await actionTask;
                        var embedding = await embeddingTask;

                        topEmail.Embedding = embedding;
                        topEmail.EmbeddingGeneratedDate = DateTime.UtcNow;
                        topEmail.Category = classification.Category;
                        topEmail.Priority = classification.Priority;
                        topEmail.ClassificationReasoning = classification.Reasoning;
                        topEmail.ClassificationConfidence = classification.Confidence;
                        topEmail.ClassifiedDate = DateTime.UtcNow;

                        var myEmail = "your-email@gmail.com";
                        topEmail.IsSentByUser = topEmail.Sender.Equals(myEmail,
                            StringComparison.OrdinalIgnoreCase);

                        foreach (var item in actionItems)
                        {
                            await _actionItemRepository.CreateAsync(item);
                            topEmail.ActionItemIds.Add(item.Id);
                        }

                        await _repository.UpdateAsync(topEmail);

                        await Notify("Classifier", "Complete",
                            $"Classified as {classification.Category} · Priority {classification.Priority}");
                        await Notify("ActionExtractor", "Complete",
                            $"Found {actionItems.Count} action items");
                        await Notify("EmbeddingService", "Complete",
                            "Embedding generated");

                        await _hubContext.Clients.All.SendAsync("ReceiveNotification",
                            $"Email ready: {topEmail.Subject}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background AI processing failed");
                        await Notify("System", "Failed",
                            $"Processing failed: {ex.Message}");
                    }
                });

                return CreatedAtAction(nameof(GetEmail),
                    new { id = topEmail.Id },
                    new
                    {
                        Message = $"Saved {emails.Count} emails from thread",
                        TopEmailId = topEmail.Id,
                        EmailCount = emails.Count,
                        Emails = emails
                    });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error parsing email: {ex.Message}");
            }
        }

        [HttpPost("upload/batch")]
        public async Task<ActionResult> UploadMultipleEmails(List<IFormFile> files)
        {
            if (files == null || !files.Any())
                return BadRequest("No files uploaded");

            var uploadedEmails = new List<Email>();
            var errors = new List<string>();

            foreach (var file in files)
            {
                if (!file.FileName.EndsWith(".eml"))
                {
                    errors.Add($"{file.FileName}: Not a .eml file");
                    continue;
                }

                try
                {
                    using var stream = file.OpenReadStream();
                    var email = await _parser.ParseEmailAsync(stream);
                    await _repository.CreateAsync(email);
                    uploadedEmails.Add(email);
                }
                catch (Exception ex)
                {
                    errors.Add($"{file.FileName}: {ex.Message}");
                }
            }

            return Ok(new
            {
                Uploaded = uploadedEmails.Count,
                Failed = errors.Count,
                Emails = uploadedEmails,
                Errors = errors
            });
        }

        // ── Read ──────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? subject = null)
        {
            var emails = await _repository.GetAllAsync();

            if (!string.IsNullOrEmpty(subject))
                emails = emails.Where(e =>
                    e.Subject.Contains(subject, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            return Ok(emails);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Email>> GetEmail(string id)
        {
            var email = await _repository.GetByIdAsync(id);
            return email == null ? NotFound() : Ok(email);
        }

        [HttpGet("sender/{sender}")]
        public async Task<ActionResult<List<Email>>> GetEmailsBySender(string sender)
        {
            var emails = await _repository.GetAllAsync();
            var filtered = emails
                .Where(e => e.Sender.Contains(sender, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return Ok(filtered);
        }

        [HttpGet("date-range")]
        public async Task<ActionResult<List<Email>>> GetEmailsByDateRange(
            [FromQuery] DateTime start,
            [FromQuery] DateTime end)
        {
            var emails = await _repository.GetAllAsync();
            var filtered = emails
                .Where(e => e.ReceivedDate >= start && e.ReceivedDate <= end)
                .ToList();
            return Ok(filtered);
        }

        [HttpGet("stats")]
        public async Task<ActionResult> GetEmailStats()
        {
            var emails = await _repository.GetAllAsync();
            var stats = new
            {
                TotalEmails = emails.Count,
                UniqueSenders = emails.Select(e => e.Sender).Distinct().Count(),
                OldestEmail = emails.MinBy(e => e.ReceivedDate)?.ReceivedDate,
                NewestEmail = emails.MaxBy(e => e.ReceivedDate)?.ReceivedDate,
                EmailsByMonth = emails
                    .GroupBy(e => new { e.ReceivedDate.Year, e.ReceivedDate.Month })
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Year)
                    .ThenByDescending(x => x.Month)
                    .ToList()
            };
            return Ok(stats);
        }

        // ── Delete ────────────────────────────────────────────────────────

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteEmail(string id)
        {
            var deleted = await _repository.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }

        // ── Search / RAG ──────────────────────────────────────────────────

        [HttpGet("search")]
        public async Task<ActionResult<List<Email>>> SearchEmails(
            [FromQuery] string query,
            [FromServices] EmailSearchService searchService)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Query cannot be empty");
            var results = await searchService.SearchAsync(query);
            return Ok(results);
        }

        [HttpGet("ask")]
        public async Task<ActionResult<RAGAnswer>> AskQuestion(
            [FromQuery] string question,
            [FromServices] EmailRAGService ragService)
        {
            if (string.IsNullOrWhiteSpace(question))
                return BadRequest("Question is required");
            var result = await ragService.AskQuestionAsync(question);
            return Ok(result);
        }

        // ── Classification ────────────────────────────────────────────────

        [HttpPost("{id}/classify")]
        public async Task<ActionResult<Email>> ClassifyEmail(
            string id,
            [FromServices] IClassifierService classifier)
        {
            var email = await _repository.GetByIdAsync(id);
            if (email == null) return NotFound();

            var result = await classifier.ClassifyEmailAsync(email);

            email.Category = result.Category;
            email.Priority = result.Priority;
            email.ClassificationReasoning = result.Reasoning;
            email.ClassificationConfidence = result.Confidence;
            email.ClassifiedDate = DateTime.UtcNow;

            await _repository.UpdateAsync(email);
            return Ok(email);
        }

        // ── Reply generation ──────────────────────────────────────────────

        [HttpPost("generate-reply/{id}")]
        public async Task<IActionResult> GenerateReply(
            string id,
            [FromServices] ReplyGenerationService replyService,
            [FromServices] ReplyValidationService validationService,
            [FromBody] ReplyOptions? options = null)
        {
            var email = await _repository.GetByIdAsync(id);
            if (email == null) return NotFound();

            var replies = await replyService.GenerateReplyAsync(email, options);
            var validated = replies.Where(r => validationService.IsValid(r, email)).ToList();
            if (!validated.Any()) validated = replies;

            return Ok(new
            {
                EmailId = id,
                GeneratedAt = DateTime.UtcNow,
                Replies = validated
            });
        }

        [HttpPost("reply-analytics")]
        public async Task<IActionResult> TrackReplyAnalytics(
            [FromBody] ReplyAnalytics entry,
            [FromServices] ReplyAnalyticsService analyticsService)
        {
            if (entry == null) return BadRequest("Entry cannot be null");
            _logger.LogInformation("Tracking analytics: {Type}", entry.SelectedType);
            await analyticsService.TrackAsync(entry);
            return Ok(new { tracked = true });
        }

        [HttpGet("reply-analytics/summary")]
        public async Task<IActionResult> GetAnalyticsSummary(
            [FromServices] ReplyAnalyticsService analyticsService)
        {
            var summary = await analyticsService.GetSummaryAsync();
            return Ok(summary);
        }

        // ── Thread summarization ──────────────────────────────────────────

        [HttpPost("summarize-thread")]
        public async Task<IActionResult> SummarizeThread(
            [FromBody] List<string> emailIds,
            [FromServices] ThreadSummarizerAgent summarizer,
            [FromServices] SummaryAnalyticsService analyticsService)
        {
            if (emailIds == null || !emailIds.Any())
                return BadRequest("Email IDs required");

            var emails = new List<Email>();
            foreach (var id in emailIds)
            {
                var email = await _repository.GetByIdAsync(id);
                if (email != null) emails.Add(email);
            }

            if (!emails.Any()) return NotFound("No emails found");

            await Notify("ThreadSummarizer", "Processing",
                $"Summarizing {emails.Count} emails...");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var summary = await summarizer.SummarizeThreadAsync(emails);
            sw.Stop();

            await analyticsService.TrackAsync(new SummaryUsageEvent
            {
                SummaryId = summary.Id,
                EmailCount = summary.OriginalEmailCount,
                ProcessingTimeSeconds = (int)sw.Elapsed.TotalSeconds,
                DecisionCount = summary.KeyDecisions.Count,
                QuestionCount = summary.OpenQuestions.Count,
                ActionItemCount = summary.ActionItems.Count,
                ConfidenceScore = summary.ConfidenceScore
            });

            await Notify("ThreadSummarizer", "Complete",
                $"Summary done — {summary.KeyDecisions.Count} decisions, " +
                $"{summary.ActionItems.Count} action items");

            return Ok(summary);
        }

        [HttpPost("summarize-by-subject")]
        public async Task<IActionResult> SummarizeBySubject(
            [FromQuery] string subject,
            [FromServices] ThreadSummarizerAgent summarizer,
            [FromServices] SummaryAnalyticsService analyticsService)
        {
            if (string.IsNullOrWhiteSpace(subject))
                return BadRequest("Subject required");

            var allEmails = await _repository.GetAllAsync();
            var baseSubject = subject
                .Replace("Re:", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Fwd:", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            var thread = allEmails
                .Where(e => e.Subject != null &&
                            e.Subject.Contains(baseSubject,
                                StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!thread.Any()) return NotFound("No emails found for this subject");
            if (thread.Count < 2)
                return BadRequest("Need at least 2 emails to summarize a thread");

            await Notify("ThreadSummarizer", "Processing",
                $"Summarizing thread: {baseSubject}");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var summary = await summarizer.SummarizeThreadAsync(thread);
            sw.Stop();

            await analyticsService.TrackAsync(new SummaryUsageEvent
            {
                SummaryId = summary.Id,
                EmailCount = summary.OriginalEmailCount,
                ProcessingTimeSeconds = (int)sw.Elapsed.TotalSeconds,
                DecisionCount = summary.KeyDecisions.Count,
                QuestionCount = summary.OpenQuestions.Count,
                ActionItemCount = summary.ActionItems.Count,
                ConfidenceScore = summary.ConfidenceScore
            });

            await Notify("ThreadSummarizer", "Complete",
                $"Summary done — confidence {summary.ConfidenceScore}%");

            return Ok(summary);
        }

        [HttpGet("summary-analytics")]
        public async Task<IActionResult> GetSummaryAnalytics(
            [FromServices] SummaryAnalyticsService analyticsService)
        {
            var analytics = await analyticsService.GetSummaryAsync();
            return Ok(analytics);
        }

        // ── Draft assistant ───────────────────────────────────────────────

        [HttpPost("compose")]
        public async Task<IActionResult> ComposeDraft(
            [FromBody] ComposeRequest request,
            [FromServices] IDraftAssistantAgent draftAgent)
        {
            if (string.IsNullOrWhiteSpace(request.Purpose))
                return BadRequest("Purpose is required.");

            await Notify("DraftAssistant", "Processing",
                $"Composing: {request.Purpose}");

            var draft = await draftAgent.ComposeEmailAsync(request);

            await Notify("DraftAssistant", "Complete",
                $"Draft ready — confidence {draft.ConfidenceScore * 100:F0}%");

            return Ok(draft);
        }

        // ── Debug ─────────────────────────────────────────────────────────

        [HttpGet("test-gemini")]
        public async Task<ActionResult> TestGemini(
            [FromServices] IGeminiClientService gemini)
        {
            var response = await gemini.GenerateContentAsync("Say hello!");
            return Ok(response);
        }

        // ── Private helpers ───────────────────────────────────────────────

        private async Task Notify(string agentName, string status, string message)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveAgentUpdate", new
                {
                    AgentName = agentName,
                    Status = status,
                    Message = message,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("SignalR notify failed: {Msg}", ex.Message);
            }
        }
    }
}