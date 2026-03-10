namespace InsightMail.API.Models
{
    public class Email
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Sender { get; set; } = string.Empty;
        public List<string> Recipients { get; set; } = new();
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? HtmlBody { get; set; }
        public DateTime ReceivedDate { get; set; }
        public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

        // For threading 
        public string? ThreadId { get; set; }
        public string? InReplyTo { get; set; }

        // AI results (added later) 
        public string? Category { get; set; }
        public int? Priority { get; set; }
    }
}
