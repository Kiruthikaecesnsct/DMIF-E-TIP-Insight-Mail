using InsightMail.API.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using InsightMail.Models;

namespace InsightMail.API.Services
{
    public interface IEmailRepository
    {
        Task<Email> CreateAsync(Email email);
        Task<Email?> GetByIdAsync(string id);
        Task<List<Email>> GetAllAsync();
        Task<bool> DeleteAsync(string id);
        Task<List<Email>> SearchAsync(string query);
        Task<List<SearchResult>> VectorSearchAsync(float[] queryEmbedding, int limit = 10);
        Task<bool> UpdateAsync(Email email);
        Task<List<Email>> GetBySenderAsync(string sender);
        Task<List<Email>> GetSentEmailsAsync();

    }

    public class EmailRepository : IEmailRepository
    {
        private readonly IMongoCollection<Email> _emails;

        public EmailRepository(MongoDbService mongoDb)
        {
            _emails = mongoDb.GetCollection<Email>("emails");
        }

        public async Task<Email> CreateAsync(Email email)
        {
            await _emails.InsertOneAsync(email);
            return email;
        }

        public async Task<Email?> GetByIdAsync(string id)
        {
            return await _emails.Find(e => e.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Email>> GetAllAsync()
        {
            return await _emails.Find(_ => true)
                .SortByDescending(e => e.ReceivedDate)
                .ToListAsync();
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _emails.DeleteOneAsync(e => e.Id == id);
            return result.DeletedCount > 0;
        }

        public async Task<List<Email>> SearchAsync(string query)
        {
            var filter = Builders<Email>.Filter.Or(
                Builders<Email>.Filter.Regex(e => e.Subject,
                    new MongoDB.Bson.BsonRegularExpression(query, "i")),
                Builders<Email>.Filter.Regex(e => e.Body,
                    new MongoDB.Bson.BsonRegularExpression(query, "i")),
                Builders<Email>.Filter.Regex(e => e.Sender,
                    new MongoDB.Bson.BsonRegularExpression(query, "i"))
            );

            return await _emails.Find(filter)
                .SortByDescending(e => e.ReceivedDate)
                .ToListAsync();
        }
        public async Task<bool> UpdateAsync(Email email)
        {
            var result = await _emails.ReplaceOneAsync(
                e => e.Id == email.Id,
                email
            );

            return result.ModifiedCount > 0;
        }
        public async Task<List<Email>> GetBySenderAsync(string sender)
        {
            return await _emails.Find(e => e.Sender == sender)
                .SortByDescending(e => e.ReceivedDate)
                .Limit(5)
                .ToListAsync();
        }

        // ⚠️ You NEED a way to identify sent emails
        // simplest: assume sender == "me@yourapp.com"

        public async Task<List<Email>> GetSentEmailsAsync()
        {
            return await _emails.Find(e => e.IsSentByUser)
                .SortByDescending(e => e.ReceivedDate)
                .Limit(5)
                .ToListAsync();
        }
        public async Task<List<SearchResult>> VectorSearchAsync(float[] queryEmbedding, int limit = 10)
        {
            var pipeline = new[]
            {
        new BsonDocument("$vectorSearch", new BsonDocument
        {
            { "index", "email_vector_index" },
            { "path", "Embedding" },
            { "queryVector", new BsonArray(queryEmbedding) },
            { "numCandidates", 100 },
            { "limit", limit }
        }),
        new BsonDocument("$project", new BsonDocument
        {
            { "_id", 0 }, // optional
            { "Email", "$$ROOT" }, // wrap full document
            { "score", new BsonDocument("$meta", "vectorSearchScore") }
        })
    };

            return await _emails
                .Aggregate<SearchResult>(pipeline)
                .ToListAsync();
        }
    }
    }