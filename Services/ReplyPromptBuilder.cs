using InsightMail.API.Models;
using System.Text;

namespace InsightMail.Services
{
    public class ReplyPromptBuilder
    {
        public string BuildPrompt(ReplyContext context)
        {
            var sb = new StringBuilder();

            // 📩 Incoming Email
            sb.AppendLine("INCOMING EMAIL:");
            sb.AppendLine($"From: {context.IncomingEmail.Sender}");
            sb.AppendLine($"Subject: {context.IncomingEmail.Subject}");
            sb.AppendLine($"Content: {context.IncomingEmail.Body}");
            sb.AppendLine();

            // 🧵 Conversation History
            if (context.ConversationHistory.Any())
            {
                sb.AppendLine("PAST CONVERSATIONS:");
                foreach (var email in context.ConversationHistory)
                {
                    sb.AppendLine($"[{email.ReceivedDate:yyyy-MM-dd}] {email.Subject}");
                    sb.AppendLine(Truncate(email.Body, 200));
                    sb.AppendLine();
                }
            }

            // 🔍 Similar Emails
            if (context.SimilarEmails.Any())
            {
                sb.AppendLine("SIMILAR EMAILS:");
                foreach (var email in context.SimilarEmails)
                {
                    sb.AppendLine(email.Subject);
                    sb.AppendLine(Truncate(email.Body, 150));
                    sb.AppendLine();
                }
            }

            // ✍️ Style
            if (context.UserStyleExamples.Any())
            {
                sb.AppendLine("YOUR WRITING STYLE:");
                foreach (var email in context.UserStyleExamples)
                {
                    sb.AppendLine(Truncate(email.Body, 150));
                    sb.AppendLine();
                }
            }

            // 🎯 Instructions
            sb.AppendLine(@"
Generate 3 email reply options as JSON. Return ONLY the JSON object, no markdown, no backticks.

{
  ""replies"": [
    {
      ""type"": ""brief"",
      ""subject"": ""Re: [subject here]"",
      ""body"": ""2-3 sentence reply here"",
      ""tone"": ""professional"",
      ""confidence"": 0.95
    },
    {
      ""type"": ""detailed"",
      ""subject"": ""Re: [subject here]"",
      ""body"": ""Full detailed reply here"",
      ""tone"": ""professional"",
      ""confidence"": 0.92
    },
    {
      ""type"": ""decline"",
      ""subject"": ""Re: [subject here]"",
      ""body"": ""Polite decline here"",
      ""tone"": ""apologetic"",
      ""confidence"": 0.85
    }
  ]
}

Rules:
- Match user tone from style examples
- Reference past conversations where relevant
- Honor any commitments found in history
- Be natural, not robotic");
            return sb.ToString();
        }

        private string Truncate(string text, int length)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= length ? text : text.Substring(0, length) + "...";
        }
    }
}