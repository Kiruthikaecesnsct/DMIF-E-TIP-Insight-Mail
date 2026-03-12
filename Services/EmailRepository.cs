using MongoDB.Driver;
using InsightMail.API.Models;

namespace InsightMail.API.Services
{
    public interface IEmailRepository
    {
        Task<Email> CreateAsync(Email email);
        Task<Email?> GetByIdAsync(string id);
        Task<List<Email>> GetAllAsync();
        Task<bool> DeleteAsync(string id);
        Task<List<Email>> SearchAsync(string query);

        Task<bool> UpdateAsync(Email email);
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
    }

}