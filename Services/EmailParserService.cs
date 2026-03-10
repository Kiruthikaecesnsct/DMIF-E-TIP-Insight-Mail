using MimeKit;
using InsightMail.API.Models;

namespace InsightMail.API.Services
{
    public interface IEmailParserService
    {
        Task<Email> ParseEmailAsync(Stream emailStream);
    }

    public class EmailParserService : IEmailParserService
    {
        public async Task<Email> ParseEmailAsync(Stream emailStream)
        {
            var message = await MimeMessage.LoadAsync(emailStream);

            var email = new Email
            {
                Sender = message.From.ToString(),
                Recipients = message.To.Select(x => x.ToString()).ToList(),
                Subject = message.Subject ?? string.Empty,
                ReceivedDate = message.Date.UtcDateTime,
                ThreadId = message.MessageId,
                InReplyTo = message.InReplyTo
            };

            // Extract body (prefer plain text over HTML) 
            if (message.TextBody != null)
            {
                email.Body = message.TextBody;
            }
            else if (message.HtmlBody != null)
            {
                email.HtmlBody = message.HtmlBody;
                email.Body = StripHtml(message.HtmlBody);
            }

            return email;
        }

        private string StripHtml(string html)
        {
            // Simple HTML stripping (use HtmlAgilityPack for production) 
            return System.Text.RegularExpressions.Regex
                .Replace(html, "<.*?>", string.Empty);
        }
    }
}
