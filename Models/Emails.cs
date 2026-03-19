using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace InsightMail.API.Models
{
    public class Email
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required(ErrorMessage = "Sender is required")]
        [EmailAddress(ErrorMessage = "Invalid email address format")]
        public string Sender { get; set; } = string.Empty;

        [Required(ErrorMessage = "At least one recipient is required")]
        [MinLength(1, ErrorMessage = "Recipients list cannot be empty")]
        public List<string> Recipients { get; set; } = new();

        [Required(ErrorMessage = "Subject is required")]
        [StringLength(500, ErrorMessage = "Subject cannot exceed 500 characters")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Body is required")]
        public string Body { get; set; } = string.Empty;

        public string? HtmlBody { get; set; }

        [Required]
        public DateTime ReceivedDate { get; set; }

        public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

        // Threading
        public string? ThreadId { get; set; }
        public string? InReplyTo { get; set; }

        // AI analysis fields
        [StringLength(100)]
        public string? Category { get; set; }

        [Range(1, 10)]
        public int? Priority { get; set; }

        public string? ClassificationReasoning { get; set; }
        public double? ClassificationConfidence { get; set; }
        public DateTime? ClassifiedDate { get; set; }
        public List<string> ActionItemIds { get; set; } = new();
        public bool HasActionItems => ActionItemIds.Any();
        public float[]? Embedding { get; set; }
        public DateTime? EmbeddingGeneratedDate { get; set; }
        [BsonElement("score")]
        [BsonIgnoreIfNull]
        public float? Score { get; set; }
        public bool IsSentByUser { get; set; } = false;

    }
}