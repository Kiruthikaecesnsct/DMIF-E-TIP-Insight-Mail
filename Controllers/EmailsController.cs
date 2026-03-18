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
using InsightMail.API.Models;
using InsightMail.API.Services;
using InsightMail.Models;
using InsightMail.Services;
using Microsoft.AspNetCore.Mvc;

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
        private readonly ILogger<EmailsController> _logger;

        public EmailsController(
            IEmailParserService parser,
            IEmailRepository repository,
            IClassifierService classifier,
            IActionExtractorService extractor,
    IActionItemRepository actionItemRepository,
    IEmbeddingService embeddingService,
    ILogger<EmailsController> logger)
        {
            _parser = parser;
            _repository = repository;
            _classifier = classifier;
            _extractor = extractor;
            _actionItemRepository = actionItemRepository;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        /// <summary>
        /// Uploads and parses a single .eml email file
        /// </summary>
        /// <param name="file">The .eml file to upload</param>
        /// <returns>The parsed email object</returns>
        [HttpPost("upload")]
        public async Task<ActionResult<Email>> UploadEmail(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            if (!file.FileName.EndsWith(".eml"))
                return BadRequest("Only .eml files are supported");

            try
            {
                using var stream = file.OpenReadStream();
                var email = await _parser.ParseEmailAsync(stream);

                await _repository.CreateAsync(email);

                // Run AI tasks in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Run ALL in parallel 🔥
                        var classificationTask = _classifier.ClassifyEmailAsync(email);
                        var actionTask = _extractor.ExtractActionItemsAsync(email);
                        var embeddingTask = _embeddingService.GenerateEmbeddingAsync(
                            $"{email.Subject} {email.Body}");

                        await Task.WhenAll(classificationTask, actionTask, embeddingTask);

                        // Assign results
                        var classification = await classificationTask;
                        var actionItems = await actionTask;
                        var embedding = await embeddingTask;

                        Console.WriteLine($"Generated embedding: {embedding.Length}");
                        // Save embedding
                        email.Embedding = embedding;
                        email.EmbeddingGeneratedDate = DateTime.UtcNow;

                        // Save classification
                        email.Category = classification.Category;
                        email.Priority = classification.Priority;
                        email.ClassificationReasoning = classification.Reasoning;
                        email.ClassificationConfidence = classification.Confidence;
                        email.ClassifiedDate = DateTime.UtcNow;

                        // Save action items
                        foreach (var item in actionItems)
                        {
                            await _actionItemRepository.CreateAsync(item);
                            email.ActionItemIds.Add(item.Id);
                        }

                        // SINGLE DB UPDATE ✅
                        await _repository.UpdateAsync(email);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background AI processing failed");
                    }
                });

                return CreatedAtAction(nameof(GetEmail), new { id = email.Id }, email);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error parsing email: {ex.Message}");
            }
        }

        /// <summary>
        /// Upload multiple email files at once
        /// </summary>
        /// <param name="files">List of .eml files</param>
        /// <returns>Upload summary with success and errors</returns>
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

        /// <summary>
        /// Retrieves all emails from the database
        /// </summary>
        /// <returns>List of emails sorted by received date</returns>
        [HttpGet]
        public async Task<ActionResult<List<Email>>> GetEmails()
        {
            var emails = await _repository.GetAllAsync();
            return Ok(emails);
        }

        /// <summary>
        /// Retrieves a single email by ID
        /// </summary>
        /// <param name="id">Email ID</param>
        /// <returns>Email object</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<Email>> GetEmail(string id)
        {
            var email = await _repository.GetByIdAsync(id);

            if (email == null)
                return NotFound();

            return Ok(email);
        }

        /// <summary>
        /// Deletes an email by ID
        /// </summary>
        /// <param name="id">Email ID</param>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteEmail(string id)
        {
            var deleted = await _repository.DeleteAsync(id);

            if (!deleted)
                return NotFound();

            return NoContent();
        }

        /// <summary>
        /// Search emails by keyword
        /// </summary>
        /// <param name="query">Search text</param>
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

        /// <summary>
        /// Get emails sent by a specific sender
        /// </summary>
        /// <param name="sender">Sender email address</param>
        [HttpGet("sender/{sender}")]
        public async Task<ActionResult<List<Email>>> GetEmailsBySender(string sender)
        {
            var emails = await _repository.GetAllAsync();

            var filtered = emails
                .Where(e => e.Sender.Contains(sender,
                StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Ok(filtered);
        }

        /// <summary>
        /// Retrieve emails within a date range
        /// </summary>
        [HttpGet("date-range")]
        public async Task<ActionResult<List<Email>>> GetEmailsByDateRange(
            [FromQuery] DateTime start,
            [FromQuery] DateTime end)
        {
            var emails = await _repository.GetAllAsync();

            var filtered = emails
                .Where(e => e.ReceivedDate >= start &&
                            e.ReceivedDate <= end)
                .ToList();

            return Ok(filtered);
        }
        [HttpGet("test-gemini")]
        public async Task<ActionResult> TestGemini(
    [FromServices] IGeminiClientService gemini)
        {
            var response = await gemini.GenerateContentAsync("Say hello!");
            return Ok(response);
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
        [HttpPost("{id}/classify")]
        public async Task<ActionResult<Email>> ClassifyEmail(
    string id,
    [FromServices] IClassifierService classifier)
        {
            var email = await _repository.GetByIdAsync(id);

            if (email == null)
                return NotFound();

            var result = await classifier.ClassifyEmailAsync(email);

            email.Category = result.Category;
            email.Priority = result.Priority;
            email.ClassificationReasoning = result.Reasoning;
            email.ClassificationConfidence = result.Confidence;
            email.ClassifiedDate = DateTime.UtcNow;

            await _repository.UpdateAsync(email);

            return Ok(email);
        }
        /// <summary>
        /// Get email analytics and statistics
        /// </summary>
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
    }
}
