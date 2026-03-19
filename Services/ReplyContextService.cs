using InsightMail.API.Models;
using InsightMail.API.Services;

namespace InsightMail.Services
{
    public class ReplyContextService
    {
        private readonly IEmailRepository _repository;
        private readonly IEmbeddingService _embedding;

        public ReplyContextService(
            IEmailRepository repository,
            IEmbeddingService embedding)
        {
            _repository = repository;
            _embedding = embedding;
        }

        public async Task<ReplyContext> BuildContextAsync(Email incomingEmail)
        {
            // Run all in parallel 🚀
            var historyTask = GetConversationHistoryAsync(incomingEmail.Sender);
            var similarTask = GetSimilarEmailsAsync(incomingEmail);
            var styleTask = GetUserResponseStyleAsync();

            await Task.WhenAll(historyTask, similarTask, styleTask);

            return new ReplyContext
            {
                IncomingEmail = incomingEmail,
                ConversationHistory = historyTask.Result,
                SimilarEmails = similarTask.Result,
                UserStyleExamples = styleTask.Result
            };
        }

        // 1️⃣ Conversation History
        private async Task<List<Email>> GetConversationHistoryAsync(string sender)
        {
            var emails = await _repository.GetBySenderAsync(sender);

            return emails
                .OrderByDescending(e => e.ReceivedDate)
                .Take(3)
                .ToList();
        }

        // 2️⃣ Similar Emails (Vector Search)
        private async Task<List<Email>> GetSimilarEmailsAsync(Email incoming)
        {
            var embedding = await _embedding
                .GenerateEmbeddingAsync($"{incoming.Subject} {incoming.Body}");

            var results = await _repository.VectorSearchAsync(embedding, 5);

            foreach (var r in results)
            {
                Console.WriteLine($"Score: {r.Score}");
                Console.WriteLine($"Sender: {r.Email.Sender}");
                Console.WriteLine($"Subject: {r.Email.Subject}");
            }

            return results
                .Where(r => r.Email.Sender != incoming.Sender) // ✅ FIXED
                .Select(r => r.Email) // ✅ extract Email
                .Take(2)
                .ToList();
        }

        // 3️⃣ User Style
        private async Task<List<Email>> GetUserResponseStyleAsync()
        {
            var sentEmails = await _repository.GetSentEmailsAsync();

            return sentEmails
                .OrderByDescending(e => e.ReceivedDate)
                .Take(3)
                .ToList();
        }
    }
}