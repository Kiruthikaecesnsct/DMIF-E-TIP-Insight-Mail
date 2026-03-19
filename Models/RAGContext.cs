namespace InsightMail.API.Models
{
    public class ReplyContext
    {
        public Email IncomingEmail { get; set; }

        public List<Email> ConversationHistory { get; set; } = new();

        public List<Email> SimilarEmails { get; set; } = new();

        public List<Email> UserStyleExamples { get; set; } = new();
    }
}