using InsightMail.API.Services;
using InsightMail.Models;
using MongoDB.Driver;

namespace InsightMail.Services
{
    public class ReplyAnalyticsService
    {
        private readonly IMongoCollection<ReplyAnalytics> _collection;
        private readonly ILogger<ReplyAnalyticsService> _logger;

        public ReplyAnalyticsService(
            MongoDbService mongoDb,
            ILogger<ReplyAnalyticsService> logger)
        {
            _collection = mongoDb.GetCollection<ReplyAnalytics>("reply_analytics");
            _logger = logger;
        }

        public async Task TrackAsync(ReplyAnalytics entry)
        {
            try
            {
                entry.EditDistance = Math.Abs(
                    entry.FinalBody.Length - entry.OriginalBody.Length);
                await _collection.InsertOneAsync(entry);
                _logger.LogInformation(
                    "Analytics tracked: {Type}", entry.SelectedType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to track analytics");
                throw;
            }
        }

        public async Task<AnalyticsSummary> GetSummaryAsync()
        {
            try
            {
                var all = await _collection.Find(_ => true).ToListAsync();
                _logger.LogInformation(
                    "Analytics records found: {Count}", all.Count);

                return new AnalyticsSummary
                {
                    TotalGenerated = all.Count,
                    BriefSelected = all.Count(a => a.SelectedType == "brief"),
                    DetailedSelected = all.Count(a => a.SelectedType == "detailed"),
                    DeclineSelected = all.Count(a => a.SelectedType == "decline"),
                    AverageEditDistance = all.Any()
                        ? all.Average(a => a.EditDistance) : 0,
                    MostUsedTone = all
                        .GroupBy(a => a.ToneUsed)
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault()?.Key ?? "N/A"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get summary");
                throw;
            }
        }
    }
}