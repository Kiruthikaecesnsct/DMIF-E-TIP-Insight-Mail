using InsightMail.Models;

namespace InsightMail.Services
{
    public interface IThreadSummaryRepository
    {
        Task<ThreadSummary?> GetByIdAsync(string id);
        Task<ThreadSummary?> GetByThreadIdAsync(string threadId);
        Task<List<ThreadSummary>> GetAllAsync();
        Task<ThreadSummary> CreateAsync(ThreadSummary summary);
        Task UpdateAsync(ThreadSummary summary);
        Task DeleteAsync(string id);
    }
}