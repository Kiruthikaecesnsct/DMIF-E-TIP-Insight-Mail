using InsightMail.Models;

namespace InsightMail.Services
{
    public class EmailTemplateService
    {
        private readonly Dictionary<string, EmailTemplate> _templates = new()
        {
            ["meeting_request"] = new EmailTemplate
            {
                Name = "Meeting Request",
                SubjectTemplate = "Meeting Request: {topic}",
                BodyTemplate = "Hi {recipient},\n\nI'd like to schedule a meeting to discuss {topic}.\n\nProposed times:\n{time_options}\n\nPlease let me know what works best for you.\n\nBest regards,",
                RequiredFields = new[] { "recipient", "topic", "time_options" }
            },
            ["follow_up"] = new EmailTemplate
            {
                Name = "Follow-up",
                SubjectTemplate = "Following up: {original_subject}",
                BodyTemplate = "Hi {recipient},\n\nI wanted to follow up on {topic} from our {previous_interaction}.\n\n{main_points}\n\nLooking forward to your response.\n\nBest regards,",
                RequiredFields = new[] { "recipient", "topic", "previous_interaction", "main_points" }
            },
            ["status_update"] = new EmailTemplate
            {
                Name = "Status Update",
                SubjectTemplate = "{project} - Status Update",
                BodyTemplate = "Hi team,\n\nQuick update on {project}:\n\nProgress: {progress}\nNext steps: {next_steps}\nBlockers: {blockers}\n\nLet me know if you have questions.\n\nBest regards,",
                RequiredFields = new[] { "project", "progress", "next_steps", "blockers" }
            }
        };

        public Dictionary<string, EmailTemplate> GetAllTemplates() => _templates;

        public EmailTemplate? GetTemplate(string key) =>
            _templates.TryGetValue(key, out var t) ? t : null;

        public EmailDraft ApplyTemplate(string templateKey,
            Dictionary<string, string> fields)
        {
            if (!_templates.TryGetValue(templateKey, out var template))
                throw new KeyNotFoundException($"Template '{templateKey}' not found.");

            var subject = template.SubjectTemplate;
            var body = template.BodyTemplate;

            foreach (var field in fields)
            {
                subject = subject.Replace($"{{{field.Key}}}", field.Value);
                body = body.Replace($"{{{field.Key}}}", field.Value);
            }

            return new EmailDraft
            {
                Subject = subject,
                Body = body,
                TemplateUsed = templateKey,
                GeneratedDate = DateTime.UtcNow
            };
        }
    }
}