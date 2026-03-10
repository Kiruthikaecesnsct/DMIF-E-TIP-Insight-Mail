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
using Microsoft.AspNetCore.Mvc;
using InsightMail.API.Services;
using InsightMail.API.Models;

namespace InsightMail.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class EmailsController : ControllerBase
    {
        private readonly IEmailParserService _parser;
        private readonly IEmailRepository _repository;

        public EmailsController(
            IEmailParserService parser,
            IEmailRepository repository)
        {
            _parser = parser;
            _repository = repository;
        }

        [HttpPost("upload")]
        public async Task<ActionResult<Email>> UploadEmail(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            if (!file.FileName.EndsWith(".eml"))
            {
                return BadRequest("Only .eml files are supported");
            }

            try
            {
                using var stream = file.OpenReadStream();
                var email = await _parser.ParseEmailAsync(stream);

                // Save to MongoDB
                await _repository.CreateAsync(email);

                return CreatedAtAction(
                    nameof(GetEmail),
                    new { id = email.Id },
                    email);
            }
            catch (Exception ex)
            {
                return StatusCode(500,
                    $"Error parsing email: {ex.Message}");
            }
        }

        [HttpPost("upload/batch")]
        public async Task<ActionResult> UploadMultipleEmails(List<IFormFile> files)
        {
            if (files == null || !files.Any())
            {
                return BadRequest("No files uploaded");
            }

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

            var result = new
            {
                Uploaded = uploadedEmails.Count,
                Failed = errors.Count,
                Emails = uploadedEmails,
                Errors = errors
            };

            return Ok(result);
        }

        [HttpGet]
        public async Task<ActionResult<List<Email>>> GetEmails()
        {
            var emails = await _repository.GetAllAsync();
            return Ok(emails);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Email>> GetEmail(string id)
        {
            var email = await _repository.GetByIdAsync(id);

            if (email == null)
                return NotFound();

            return Ok(email);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteEmail(string id)
        {
            var deleted = await _repository.DeleteAsync(id);

            if (!deleted)
                return NotFound();

            return NoContent();
        }
    }
}
