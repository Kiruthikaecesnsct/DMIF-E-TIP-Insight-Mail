using InsightMail.Models;
using InsightMail.Services;
using MongoDB.Driver;

namespace InsightMail.Repositories
{
    public class ThreadSummaryRepository : IThreadSummaryRepository
    {
        private readonly IMongoCollection<ThreadSummary> _collection;

        public ThreadSummaryRepository(IConfiguration config)
        {
            var client = new MongoClient(config["MongoDB:ConnectionString"]);
            var db = client.GetDatabase(config["MongoDB:DatabaseName"]);
            _collection = db.GetCollection<ThreadSummary>("thread_summaries");
        }

        public async Task<ThreadSummary?> GetByIdAsync(string id) =>
            await _collection.Find(s => s.Id == id).FirstOrDefaultAsync();

        public async Task<ThreadSummary?> GetByThreadIdAsync(string threadId) =>
            await _collection.Find(s => s.ThreadId == threadId).FirstOrDefaultAsync();

        public async Task<List<ThreadSummary>> GetAllAsync() =>
            await _collection.Find(_ => true).ToListAsync();

        public async Task<ThreadSummary> CreateAsync(ThreadSummary summary)
        {
           // summary.Id = Guid.NewGuid().ToString();
            await _collection.InsertOneAsync(summary);
            return summary;
        }

        public async Task UpdateAsync(ThreadSummary summary) =>
            await _collection.ReplaceOneAsync(s => s.Id == summary.Id, summary);

        public async Task DeleteAsync(string id) =>
            await _collection.DeleteOneAsync(s => s.Id == id);
    }
}