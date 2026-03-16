using InsightMail.API.Models;
using InsightMail.Models;

namespace InsightMail.Services
{
    public interface IActionExtractorService
    {
        Task<List<ActionItem>> ExtractActionItemsAsync(Email email);
    }

}
