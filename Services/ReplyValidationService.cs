using InsightMail.API.Models;
using InsightMail.Models;

namespace InsightMail.Services
{
    public class ReplyValidationService
    {
        public bool IsValid(EmailReply reply, Email incoming)
        {
            if (string.IsNullOrWhiteSpace(reply.Body) || reply.Body.Length < 20)
                return false;

            // Didn't just echo the incoming email back
            var similarity = ComputeOverlap(reply.Body, incoming.Body);
            if (similarity > 0.8) return false;

            // Has some kind of closing
            var lower = reply.Body.ToLower();
            var hasClosing = lower.Contains("regards") || lower.Contains("thanks") ||
                             lower.Contains("sincerely") || lower.Contains("best");
            if (!hasClosing) return false;

            return true;
        }

        private double ComputeOverlap(string a, string b)
        {
            var wordsA = a.Split(' ').ToHashSet();
            var wordsB = b.Split(' ').ToHashSet();
            var intersection = wordsA.Intersect(wordsB).Count();
            return (double)intersection / Math.Max(wordsA.Count, wordsB.Count);
        }
    }
}