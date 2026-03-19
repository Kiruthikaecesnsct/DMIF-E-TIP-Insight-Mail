using InsightMail.API.Models;

namespace InsightMail.Models
{
    public class ReplyContext
    {
        public Email IncomingEmail { get; set; } = null!;
        public List<Email> ConversationHistory { get; set; } = new();
        public List<Email> SimilarEmails { get; set; } = new();
        public List<Email> UserStyleExamples { get; set; } = new();
    }
}