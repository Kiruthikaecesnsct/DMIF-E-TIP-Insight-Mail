using InsightMail.API.Models;
using InsightMail.API.Services;
using InsightMail.Models;
using System.Text.Json;

namespace InsightMail.Services
{
    public class ActionExtractorService : IActionExtractorService
    {
        private readonly IGeminiClientService _gemini;
        private readonly ILogger<ActionExtractorService> _logger;

        public ActionExtractorService(
            IGeminiClientService gemini,
            ILogger<ActionExtractorService> logger)
        {
            _gemini = gemini;
            _logger = logger;
        }

        public async Task<List<ActionItem>> ExtractActionItemsAsync(Email email)
        {
            try
            {
                var prompt = BuildExtractionPrompt(email);
                var response = await _gemini.GenerateContentAsync(prompt);
                var items = ParseActionItems(response, email.Id);

                _logger.LogInformation(
                    "Extracted {Count} action items from email {EmailId}",
                    items.Count, email.Id);

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to extract action items from email {EmailId}",
                    email.Id);
                return new List<ActionItem>();
            }
        }

        private string BuildExtractionPrompt(Email email)
        {
            return $@"You are an action item extraction assistant. 
 
Extract all tasks, to-dos, and action items from this email. 
 
For each item: 
- description: Clear, specific action 
- assignedTo: Person's name or 'Email Recipient' 
- dueDate: YYYY-MM-DD format or null 
- priority: High (urgent), Medium (normal), Low (tentative) 
- confidence: 0.0-1.0 (certainty this is a task) 
 
Guidelines: 
- Look for action verbs: send, review, prepare, schedule, update 
- If deadline vague ('soon', 'ASAP'), estimate 3 days 
- Include tentative tasks but mark low priority 
- Ignore greetings, signatures, pure information 
 
Email Subject: {email.Subject} 
From: {email.Sender} 
Body: 
{TruncateBody(email.Body, 1500)} 
 
Return ONLY valid JSON array: 
[ 
  {{ 
    ""description"": ""action"", 
    ""assignedTo"": ""name"", 
    ""dueDate"": ""2024-03-20"", 
    ""priority"": ""High"", 
    ""confidence"": 0.9 
  }} 
] 
 
If no action items: []";
        }

        private List<ActionItem> ParseActionItems(string response, string emailId)
        {
            try
            {
                // Clean response 
                var cleaned = response.Trim()
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                // Find JSON array 
                var startIndex = cleaned.IndexOf('[');
                var endIndex = cleaned.LastIndexOf(']');

                if (startIndex < 0 || endIndex < 0)
                    return new List<ActionItem>();

                cleaned = cleaned.Substring(
                    startIndex,
                    endIndex - startIndex + 1);

                // Parse JSON 
                var extracted = JsonSerializer.Deserialize<List<ExtractedItem>>(
                    cleaned,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                // Convert to ActionItem entities 
                return extracted?.Select(e => new ActionItem
                {
                    EmailId = emailId,
                    Description = e.Description,
                    AssignedTo = e.AssignedTo ?? "Email Recipient",
                    DueDate = ParseDueDate(e.DueDate),
                    Priority = ParsePriority(e.Priority),
                    ConfidenceScore = e.Confidence,
                    ExtractedDate = DateTime.UtcNow,
                    Status = ActionItemStatus.Extracted
                }).ToList() ?? new List<ActionItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse action items");
                return new List<ActionItem>();
            }
        }

        private DateTime? ParseDueDate(string? dateStr)
        {
            if (string.IsNullOrEmpty(dateStr) || dateStr == "null")
                return null;

            return DateTime.TryParse(dateStr, out var date) ? date : null;
        }

        private Priority ParsePriority(string? priority)
        {
            return priority?.ToLower() switch
            {
                "high" => Priority.High,
                "low" => Priority.Low,
                _ => Priority.Medium
            };
        }

        private string TruncateBody(string body, int maxLength)
        {
            if (string.IsNullOrEmpty(body)) return string.Empty;
            return body.Length <= maxLength
                ? body
                : body.Substring(0, maxLength) + "...";
        }
    }

    // Helper class for JSON deserialization 
    internal class ExtractedItem
    {
        public string Description { get; set; } = string.Empty;
        public string? AssignedTo { get; set; }
        public string? DueDate { get; set; }
        public string? Priority { get; set; }
        public double Confidence { get; set; }
    }

}
