using InsightMail.API.Models;

namespace InsightMail.API.Services
{
    public class PromptService
    {
        public string GetClassificationPrompt(Email email)
        {
            var template = File.ReadAllText("Prompts/ClassificationPrompt.txt");

            return template
                .Replace("{{subject}}", email.Subject)
                .Replace("{{sender}}", email.Sender)
                .Replace("{{body}}", email.Body)
                .Replace("{{categories}}", GetCategoriesDefinition())
                .Replace("{{output_format}}", GetOutputFormat());
        }

        private string GetCategoriesDefinition()
        {
            return @"Urgent
Action Required
FYI
Newsletter";
        }

        private string GetOutputFormat()
        {
            return @"{
  ""category"": ""string"",
  ""priority"": 1-10,
  ""reasoning"": ""string"",
  ""confidence"": 0.0-1.0
}";
        }
    }
}