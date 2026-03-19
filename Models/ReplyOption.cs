namespace InsightMail.Models
{
    public class ReplyOptions
    {
        public string PreferredTone { get; set; } = "Professional";
        public bool IncludeDeclineOption { get; set; } = true;
        public int MaxLength { get; set; } = 300;
        public string? CustomInstructions { get; set; }
    }
}