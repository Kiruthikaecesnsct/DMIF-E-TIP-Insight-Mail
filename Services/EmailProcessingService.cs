using InsightMail.API.Hubs;
using InsightMail.Models;
using Microsoft.AspNetCore.SignalR;

namespace InsightMail.Services
{
    public class EmailProcessingService
    {
        private readonly IHubContext<EmailHub> _hubContext;
        private readonly ILogger<EmailProcessingService> _logger;

        public EmailProcessingService(
            IHubContext<EmailHub> hubContext,
            ILogger<EmailProcessingService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyAgentStarted(string agentName, string message)
        {
            await _hubContext.Clients.All.SendAsync("ReceiveAgentUpdate", new AgentUpdate
            {
                AgentName = agentName,
                Status = "Processing",
                Message = message,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task NotifyAgentCompleted(string agentName, string message)
        {
            await _hubContext.Clients.All.SendAsync("ReceiveAgentUpdate", new AgentUpdate
            {
                AgentName = agentName,
                Status = "Complete",
                Message = message,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task NotifyAgentFailed(string agentName, string message)
        {
            await _hubContext.Clients.All.SendAsync("ReceiveAgentUpdate", new AgentUpdate
            {
                AgentName = agentName,
                Status = "Failed",
                Message = message,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task NotifyComplete(string message)
        {
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", message);
        }
    }
}