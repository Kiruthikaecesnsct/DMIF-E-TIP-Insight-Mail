using Microsoft.AspNetCore.SignalR;

namespace InsightMail.API.Hubs
{
    public class AgentUpdate
    {
        public string AgentName { get; set; } = "";
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    public class EmailHub : Hub
    {
        public async Task SendNotification(string userId, string message)
        {
            await Clients.User(userId).SendAsync("ReceiveNotification", message);
        }

        public async Task SendAgentUpdate(string userId, AgentUpdate update)
        {
            await Clients.User(userId).SendAsync("ReceiveAgentUpdate", update);
        }

        // Broadcast to ALL connected clients (simpler for demo — no auth needed)
        public async Task BroadcastAgentUpdate(AgentUpdate update)
        {
            await Clients.All.SendAsync("ReceiveAgentUpdate", update);
        }

        public async Task BroadcastNotification(string message)
        {
            await Clients.All.SendAsync("ReceiveNotification", message);
        }
    }
}