namespace InsightMail.Models
{
    public class EmailReply
    {
        public string Type { get; set; } = "";        // "brief", "detailed", "decline"
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public string Tone { get; set; } = "";
        public double Confidence { get; set; }
        public string EmailId { get; set; } = "";
        public DateTime GeneratedDate { get; set; }
        public List<string> ContextSources { get; set; } = new();
    }
}