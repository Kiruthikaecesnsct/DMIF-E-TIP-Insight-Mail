using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InsightMail.Models
{
    public class ReplyAnalytics
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
        public string EmailId { get; set; } = "";
        public string SelectedType { get; set; } = "";
        public string OriginalBody { get; set; } = "";
        public string FinalBody { get; set; } = "";
        public int EditDistance { get; set; }
        public string ToneUsed { get; set; } = "";
        public int MaxWordsUsed { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AnalyticsSummary
    {
        public int TotalGenerated { get; set; }
        public int BriefSelected { get; set; }
        public int DetailedSelected { get; set; }
        public int DeclineSelected { get; set; }
        public double AverageEditDistance { get; set; }
        public string MostUsedTone { get; set; } = "";
    }
}