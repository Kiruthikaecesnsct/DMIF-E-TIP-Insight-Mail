using InsightMail.API.Services;
using InsightMail.Models;
using MongoDB.Driver;

namespace InsightMail.Services
{
    public interface IActionItemRepository
    {
        Task<ActionItem?> GetByIdAsync(string id);
        Task<List<ActionItem>> GetByEmailIdAsync(string emailId);
        Task<List<ActionItem>> GetByStatusAsync(ActionItemStatus status);
        Task<ActionItem> CreateAsync(ActionItem item);
        Task UpdateAsync(ActionItem item);
        Task DeleteAsync(string id);
    }
    public class ActionItemRepository : IActionItemRepository
    {
        private readonly IMongoCollection<ActionItem> _collection;

        public ActionItemRepository(MongoDbService mongo)
        {
            _collection = mongo.GetCollection<ActionItem>("actionitems");
        }

        public async Task<ActionItem?> GetByIdAsync(string id)
        {
            return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<ActionItem>> GetByEmailIdAsync(string emailId)
        {
            return await _collection.Find(x => x.EmailId == emailId).ToListAsync();
        }

        public async Task<List<ActionItem>> GetByStatusAsync(ActionItemStatus status)
        {
            return await _collection.Find(x => x.Status == status).ToListAsync();
        }

        public async Task<ActionItem> CreateAsync(ActionItem item)
        {
            await _collection.InsertOneAsync(item);
            return item;
        }

        public async Task UpdateAsync(ActionItem item)
        {
            await _collection.ReplaceOneAsync(x => x.Id == item.Id, item);
        }

        public async Task DeleteAsync(string id)
        {
            await _collection.DeleteOneAsync(x => x.Id == id);
        }
    }

}
