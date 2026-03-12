using InsightMail.API.Models;

namespace InsightMail.API.Services
{
    public interface IClassifierService
    {
        Task<EmailClassification> ClassifyEmailAsync(Email email);
    }

    public class EmailClassification
    {
        public string Category { get; set; } = string.Empty;
        public int Priority { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }
}