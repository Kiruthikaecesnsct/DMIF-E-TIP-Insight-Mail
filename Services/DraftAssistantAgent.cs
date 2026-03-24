using InsightMail.API.Models;
using InsightMail.API.Services;
using InsightMail.Models;
using InsightMail.Repositories;
using System.Text;
using System.Text.Json;

namespace InsightMail.Services
{
    public class DraftAssistantAgent : IDraftAssistantAgent
    {
        private readonly IGeminiClientService _gemini;
        private readonly IEmailRepository _emailRepo;
        private readonly ILogger<DraftAssistantAgent> _logger;

        public DraftAssistantAgent(
            IGeminiClientService gemini,
            IEmailRepository emailRepo,
            ILogger<DraftAssistantAgent> logger)
        {
            _gemini = gemini;
            _emailRepo = emailRepo;
            _logger = logger;
        }

        public async Task<EmailDraft> ComposeEmailAsync(ComposeRequest request)
        {
            // Step 1: Gather context via RAG
            var context = await GatherComposeContextAsync(request);

            // Step 2: Build prompt
            var prompt = BuildComposePrompt(request, context);

            // Step 3: Generate
            var response = await _gemini.GenerateContentAsync(prompt);

            // Step 4: Parse and return
            var draft = ParseDraft(response);
            draft.GeneratedDate = DateTime.UtcNow;

            return draft;
        }

        private async Task<ComposeContext> GatherComposeContextAsync(ComposeRequest request)
        {
            // Search relevant past emails by keyword matching on purpose + recipients
            var allEmails = await _emailRepo.GetAllAsync();
            var searchTerms = request.Purpose.ToLower().Split(' ')
                .Concat(request.Recipients.Select(r => r.ToLower()))
                .ToHashSet();

            var relevant = allEmails
                .Where(e => searchTerms.Any(term =>
                    e.Subject.ToLower().Contains(term) ||
                    e.Body.ToLower().Contains(term) ||
                    e.Sender.ToLower().Contains(term)))
                .OrderByDescending(e => e.ReceivedDate)
                .Take(3)
                .ToList();

            // Style examples: recent emails sent by user (simple heuristic)
            var styleExamples = allEmails
                .OrderByDescending(e => e.ReceivedDate)
                .Take(3)
                .ToList();

            return new ComposeContext
            {
                RelevantPastEmails = relevant,
                UserStyleExamples = styleExamples
            };
        }

        private string BuildComposePrompt(ComposeRequest request, ComposeContext context)
        {
            var pastEmails = FormatPastEmails(context.RelevantPastEmails);
            var styleExamples = FormatStyleExamples(context.UserStyleExamples);
            var keyPoints = request.KeyPoints.Any()
                ? string.Join(", ", request.KeyPoints)
                : "None specified";

            return $@"
You are composing a new email for the user.

PURPOSE: {request.Purpose}
RECIPIENTS: {string.Join(", ", request.Recipients)}
KEY POINTS TO COVER: {keyPoints}
PREFERRED TONE: {request.Tone}

RELEVANT PAST CONVERSATIONS:
{pastEmails}

USER'S WRITING STYLE EXAMPLES:
{styleExamples}

Compose a professional email that:
1. Has an appropriate subject line
2. Opens with a proper greeting
3. Covers all key points clearly
4. References past conversations naturally if relevant
5. Matches the user's writing style from examples
6. Closes professionally
7. Includes a clear call-to-action if needed

Return ONLY a JSON object, no markdown, no explanation:
{{
  ""subject"": ""email subject line"",
  ""body"": ""full email body text"",
  ""suggestedAttachments"": [],
  ""tone"": ""assessed tone"",
  ""confidenceScore"": 0.85
}}";
        }

        private string FormatPastEmails(List<Email> emails)
        {
            if (!emails.Any()) return "No relevant past emails found.";
            var sb = new StringBuilder();
            foreach (var e in emails)
            {
                sb.AppendLine($"--- {e.ReceivedDate:MMM dd} from {e.Sender} ---");
                sb.AppendLine($"Subject: {e.Subject}");
                sb.AppendLine($"Body: {e.Body[..Math.Min(200, e.Body.Length)]}...");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private string FormatStyleExamples(List<Email> emails)
        {
            if (!emails.Any()) return "No style examples available.";
            var sb = new StringBuilder();
            foreach (var e in emails.Take(2))
            {
                sb.AppendLine($"Example: {e.Body[..Math.Min(150, e.Body.Length)]}...");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private EmailDraft ParseDraft(string raw)
        {
            try
            {
                var clean = raw.Replace("```json", "").Replace("```", "").Trim();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<EmailDraft>(clean, options);
                return result ?? FallbackDraft(raw);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Draft parse failed: {Msg}", ex.Message);
                return FallbackDraft(raw);
            }
        }

        private EmailDraft FallbackDraft(string raw) => new()
        {
            Subject = "Draft Email",
            Body = raw,
            Tone = "Professional",
            ConfidenceScore = 0.5m
        };
    }
}