using InsightMail.API.Services;
using InsightMail.Models;
using MongoDB.Driver;

namespace InsightMail.Services
{
    public class SummaryAnalyticsService
    {
        private readonly IMongoCollection<SummaryUsageEvent> _collection;
        private readonly ILogger<SummaryAnalyticsService> _logger;

        public SummaryAnalyticsService(
            MongoDbService mongoDb,
            ILogger<SummaryAnalyticsService> logger)
        {
            _collection = mongoDb.GetCollection<SummaryUsageEvent>("summary_analytics");
            _logger = logger;
        }

        public async Task TrackAsync(SummaryUsageEvent evt)
        {
            try
            {
                await _collection.InsertOneAsync(evt);
                _logger.LogInformation("Summary analytics tracked");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to track summary analytics");
            }
        }

        public async Task<SummaryAnalytics> GetSummaryAsync()
        {
            var all = await _collection.Find(_ => true).ToListAsync();

            return new SummaryAnalytics
            {
                TotalSummariesGenerated = all.Count,
                AverageThreadLength = all.Any()
                    ? (int)all.Average(a => a.EmailCount) : 0,
                AverageProcessingTimeSeconds = all.Any()
                    ? (int)all.Average(a => a.ProcessingTimeSeconds) : 0,
                AverageConfidenceScore = all.Any()
                    ? all.Average(a => a.ConfidenceScore) : 0
            };
        }
    }
}