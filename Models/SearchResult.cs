using InsightMail.API.Models;
using MongoDB.Bson.Serialization.Attributes;

namespace InsightMail.Models
{
    public class SearchResult
    {
        public Email Email { get; set; }
        [BsonElement("score")]
        [BsonIgnoreIfNull]
        public float? Score { get; set; }
    }
}
