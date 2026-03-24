using InsightMail.API.Models;

namespace InsightMail.Models
{
    public class EmailDraft
    {
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public string Tone { get; set; } = "";
        public decimal ConfidenceScore { get; set; }
        public List<string> SuggestedAttachments { get; set; } = new();
        public string? TemplateUsed { get; set; }
        public DateTime GeneratedDate { get; set; }
    }

    public class ComposeRequest
    {
        public string Purpose { get; set; } = "";
        public List<string> Recipients { get; set; } = new();
        public List<string> KeyPoints { get; set; } = new();
        public string Tone { get; set; } = "Professional";
        public List<string> ReferenceEmailIds { get; set; } = new();
    }

    public class ComposeContext
    {
        public List<Email> RelevantPastEmails { get; set; } = new();
        public List<Email> UserStyleExamples { get; set; } = new();
    }

    public class EmailTemplate
    {
        public string Name { get; set; } = "";
        public string SubjectTemplate { get; set; } = "";
        public string BodyTemplate { get; set; } = "";
        public string[] RequiredFields { get; set; } = Array.Empty<string>();
    }
}