namespace InsightMail.Models
{
    public class ActionItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string EmailId { get; set; } = string.Empty;

        // Core task info 
        public string Description { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = "Email Recipient";
        public DateTime? DueDate { get; set; }
        public Priority Priority { get; set; }

        // AI metadata 
        public double ConfidenceScore { get; set; }
        public DateTime ExtractedDate { get; set; }

        // Task management 
        public ActionItemStatus Status { get; set; }
        public string? ConfirmedBy { get; set; }
        public DateTime? ConfirmedDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        // Notes 
        public string? UserNotes { get; set; }
    }

    public enum Priority
    {
        Low = 1,
        Medium = 2,
        High = 3
    }

    public enum ActionItemStatus
    {
        Extracted,      // AI found it 
        Confirmed,      // User confirmed it's valid 
        InProgress,     // User is working on it 
        Completed,      // Task done 
        Dismissed       // User says not a real task 
    }

}
