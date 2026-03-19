using System.Text.Json;
using InsightMail.API.Models;
using InsightMail.API.Services;
using InsightMail.Models;

namespace InsightMail.Services
{
    public class ReplyGenerationService
    {
        private readonly ReplyContextService _contextService;
        private readonly ReplyPromptBuilder _promptBuilder;
        private readonly IGeminiClientService _gemini;

        public ReplyGenerationService(
            ReplyContextService contextService,
            ReplyPromptBuilder promptBuilder,
            IGeminiClientService gemini)
        {
            _contextService = contextService;
            _promptBuilder = promptBuilder;
            _gemini = gemini;
        }

        public async Task<List<EmailReply>> GenerateReplyAsync(Email incomingEmail, ReplyOptions? options = null)
        {
            var context = await _contextService.BuildContextAsync(incomingEmail);
            var prompt = _promptBuilder.BuildPrompt(context);
            var rawResponse = await _gemini.GenerateContentAsync(prompt);

            var replies = ParseReplies(rawResponse);

            foreach (var reply in replies)
            {
                reply.EmailId = incomingEmail.Id;
                reply.GeneratedDate = DateTime.UtcNow;
                reply.ContextSources = context.ConversationHistory
                    .Select(e => e.Id)
                    .ToList();
            }

            return replies;
        }

        private List<EmailReply> ParseReplies(string raw)
        {
            try
            {
                // Strip markdown fences if Gemini adds them
                var clean = raw
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                using var doc = JsonDocument.Parse(clean);
                var repliesArray = doc.RootElement.GetProperty("replies");

                return repliesArray.EnumerateArray().Select(r => new EmailReply
                {
                    Type = r.GetProperty("type").GetString() ?? "",
                    Subject = r.GetProperty("subject").GetString() ?? "",
                    Body = r.GetProperty("body").GetString() ?? "",
                    Tone = r.GetProperty("tone").GetString() ?? "",
                    Confidence = r.GetProperty("confidence").GetDouble()
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Reply parse failed: {ex.Message}");
                // Fallback: return raw as a single reply so nothing breaks
                return new List<EmailReply>
                {
                    new EmailReply { Type = "brief", Body = raw, Tone = "unknown" }
                };
            }
        }
    }
}