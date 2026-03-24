using InsightMail.Models;

namespace InsightMail.Services
{
    public interface IDraftAssistantAgent
    {
        Task<EmailDraft> ComposeEmailAsync(ComposeRequest request);
    }
}