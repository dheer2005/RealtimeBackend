using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using RealtimeChat.Context;
using RealtimeChat.Models;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace RealtimeChat.Hubs
{
    public class VideoChatHub : Hub
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ChatDbContext _context;

        private static readonly ConcurrentDictionary<string, string> _userConnections = new();

        private static readonly ConcurrentDictionary<string, string> _activeCalls = new();

        public VideoChatHub(UserManager<AppUser> userManager, ChatDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            var userName = GetCurrentUserName();
            if (!string.IsNullOrEmpty(userName))
            {
                _userConnections[userName] = Context.ConnectionId;
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userName = GetCurrentUserName();
            if (!string.IsNullOrEmpty(userName))
            {
                _userConnections.TryRemove(userName, out _);

                if (_activeCalls.TryGetValue(userName, out var otherUser))
                {
                    _activeCalls.TryRemove(userName, out _);
                    _activeCalls.TryRemove(otherUser, out _);

                    if (_userConnections.TryGetValue(otherUser, out var otherConnectionId))
                    {
                        await Clients.Client(otherConnectionId).SendAsync("CallEnded", userName);
                    }
                }

            }
            await base.OnDisconnectedAsync(exception);
        }

        private string GetCurrentUserName()
        {
            return Context.User?.FindFirst(ClaimTypes.Name)?.Value
                   ?? Context.User?.FindFirst("unique_name")?.Value
                   ?? Context.User?.Identity?.Name
                   ?? string.Empty;
        }

        public async Task SendOffer(string toUser, string offer)
        {
            var fromUser = GetCurrentUserName();

            if (_activeCalls.ContainsKey(fromUser))
            {
                await Clients.Caller.SendAsync("CallFailed", "You are already in a call");
                return;
            }

            if (!_userConnections.TryGetValue(toUser, out var connectionId))
            {
                await Clients.Caller.SendAsync("CallFailed", "User is not available");
                return;
            }

            if (_activeCalls.ContainsKey(toUser))
            {
                await Clients.Caller.SendAsync("CallFailed", "User is busy on another call");
                return;
            }

            _activeCalls[fromUser] = toUser;
            _activeCalls[toUser] = fromUser;

            await Clients.Client(connectionId).SendAsync("ReceiveOffer", fromUser, offer);
        }

        public async Task SendAnswer(string toUser, string answer)
        {
            var fromUser = GetCurrentUserName();

            if (!_activeCalls.TryGetValue(fromUser, out var expectedUser) || expectedUser != toUser)
            {
                return;
            }

            if (_userConnections.TryGetValue(toUser, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync("ReceiveAnswer", fromUser, answer);
            }
            else
            {
                Console.WriteLine($"❌ Failed to send video answer to {toUser}");
            }
        }

        public async Task SendIceCandidate(string toUser, string candidate)
        {
            var fromUser = GetCurrentUserName();

            if (!_activeCalls.TryGetValue(fromUser, out var expectedUser) || expectedUser != toUser)
            {
                return;
            }

            if (_userConnections.TryGetValue(toUser, out var connectionId))
            {
                var candidateObj = System.Text.Json.JsonSerializer.Deserialize<object>(candidate);
                await Clients.Client(connectionId).SendAsync("ReceiveCandidate", fromUser, candidateObj);
            }
            else
            {
                Console.WriteLine($"❌ Failed to send video ICE candidate to {toUser}");
            }
        }

        public async Task EndCall(string toUser)
        {
            var fromUser = GetCurrentUserName();

            _activeCalls.TryRemove(fromUser, out _);
            _activeCalls.TryRemove(toUser, out _);

            if (_userConnections.TryGetValue(toUser, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync("CallEnded", fromUser);
            }

            await Clients.Caller.SendAsync("CallEnded", toUser);
        }

        public async Task DeclineCall(string fromUser)
        {
            var toUser = GetCurrentUserName();

            _activeCalls.TryRemove(fromUser, out _);
            _activeCalls.TryRemove(toUser, out _);

            if (_userConnections.TryGetValue(fromUser, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync("CallDeclined", toUser);
            }
        }

        public async Task<bool> CheckUserAvailability(string userName)
        {
            var isOnline = _userConnections.ContainsKey(userName);
            var isBusy = _activeCalls.ContainsKey(userName);

            return isOnline && !isBusy;
        }
    }
}