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
    }
}
