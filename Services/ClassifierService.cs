using InsightMail.API.Models;
using System.Text.Json;

namespace InsightMail.API.Services
{
    public class ClassifierService : IClassifierService
    {
        private readonly IGeminiClientService _gemini;
        private readonly ILogger<ClassifierService> _logger;

        public ClassifierService(
            IGeminiClientService gemini,
            ILogger<ClassifierService> logger)
        {
            _gemini = gemini;
            _logger = logger;
        }

        public async Task<EmailClassification> ClassifyEmailAsync(Email email)
        {
            try
            {
                // Build the prompt 
                var prompt = BuildClassificationPrompt(email);

                // Call Gemini 
                var response = await _gemini.GenerateContentAsync(prompt);

                // Parse response 
                var classification = ParseClassification(response);

                // Log for debugging 
                _logger.LogInformation(
                    "Classified email {EmailId}: {Category}, Priority {Priority}",
                    email.Id, classification.Category, classification.Priority
                );

                return classification;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error classifying email {EmailId}", email.Id);

                // Return safe default 
                return new EmailClassification
                {
                    Category = "FYI",
                    Priority = 5,
                    Reasoning = "Classification failed - manual review needed",
                    Confidence = 0
                };
            }
        }

        private string BuildClassificationPrompt(Email email)
        {
            return $@"You are an email classification assistant for busy executives. 
 
Your task is to categorize emails and assign priorities. 
 
Categories (choose ONE): 
- Urgent: Requires immediate attention, deadline within 24-48 hours 
- Action Required: Needs response or action, but not time-critical 
- FYI: Informational only, no action needed 
- Newsletter: Marketing, promotional, or mass emails 
 
Priority Scale (1-10): 
- 9-10: Critical, must handle today 
- 7-8: Important, handle this week 
- 5-6: Normal priority 
- 3-4: Low priority, can wait 
- 1-2: Very low priority 
 
Email to classify: 
 
Subject: {email.Subject} 
From: {email.Sender} 
Date: {email.ReceivedDate:yyyy-MM-dd HH:mm} 
 
Body: 
{TruncateBody(email.Body, 1000)} 
 
Respond ONLY with valid JSON in this exact format (no markdown, no extra text): 
{{ 
  ""category"": ""category name"", 
  ""priority"": 7, 
  ""reasoning"": ""brief explanation"", 
  ""confidence"": 0.9 
}}";
        }

        private string TruncateBody(string body, int maxLength)
        {
            if (string.IsNullOrEmpty(body)) return string.Empty;

            return body.Length <= maxLength
                ? body
                : body.Substring(0, maxLength) + "...";
        }

        private EmailClassification ParseClassification(string response)
        {
            try
            {
                // Clean up response - Gemini sometimes adds markdown 
                var cleaned = response
                    .Trim()
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                // Find JSON in response (in case there's extra text) 
                var startIndex = cleaned.IndexOf('{');
                var endIndex = cleaned.LastIndexOf('}');

                if (startIndex >= 0 && endIndex > startIndex)
                {
                    cleaned = cleaned.Substring(
                        startIndex,
                        endIndex - startIndex + 1
                    );
                }

                var result = JsonSerializer.Deserialize<EmailClassification>(
                    cleaned,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }
                );

                return result ?? throw new Exception("Failed to parse JSON");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse classification: {Response}",
                    response);
                throw;
            }
        }
    }
}
