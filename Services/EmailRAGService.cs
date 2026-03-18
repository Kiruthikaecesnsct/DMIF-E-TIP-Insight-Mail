using InsightMail.API.Models;
using InsightMail.API.Services;
using InsightMail.Models;
using System.Text;

namespace InsightMail.Services
{
    public class EmailRAGService
    {
        private readonly EmailSearchService _search;
        private readonly IGeminiClientService _gemini;

        public EmailRAGService(
            EmailSearchService search,
            IGeminiClientService gemini)
        {
            _search = search;
            _gemini = gemini;
        }

        public async Task<RAGAnswer> AskQuestionAsync(
            string question,
            int contextEmailsLimit = 5)
        {
            // 1. Get relevant emails (WITH scores)
            var results = await _search.SearchAsync(question, contextEmailsLimit);

            if (!results.Any())
            {
                return new RAGAnswer
                {
                    Answer = "I couldn't find any relevant emails.",
                    SourceEmails = new List<Email>(),
                    RelevanceScores = new List<float?>()
                };
            }

            // 2. Build prompt
            var prompt = BuildRAGPrompt(question, results);

            // 3. Generate answer
            var answer = await _gemini.GenerateContentAsync(prompt);

            // 4. Return structured response
            return new RAGAnswer
            {
                Answer = answer,
                SourceEmails = results.Select(r => r.Email).ToList(),
                RelevanceScores = results.Select(r => r.Score).ToList()
            };
        }

        private string BuildRAGPrompt(
            string question,
            List<SearchResult> results)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < results.Count; i++)
            {
                var email = results[i].Email;
                var score = results[i].Score ?? 0;

                var body = string.IsNullOrEmpty(email.Body)
                    ? ""
                    : email.Body.Substring(0, Math.Min(500, email.Body.Length));

                sb.AppendLine($@"
Email {i + 1} (Relevance: {score:P0})
From: {email.Sender}
Date: {email.ReceivedDate:yyyy-MM-dd}
Subject: {email.Subject}
Content: {body}
---");
            }

            return $@"
You are an intelligent email assistant.

User's question:
{question}

Here are relevant emails from the user's inbox:

{sb}

Instructions:
1. Answer ONLY using these emails
2. If the answer is not present, say: ""I couldn't find this in your emails.""
3. Cite sources like (Email 1), (Email 2)
4. Be concise but clear
5. If multiple emails differ, mention it

Answer:";
        }
    }
}