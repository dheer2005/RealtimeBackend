using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using RealtimeChat.Context;
using RealtimeChat.Models;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace RealtimeChat.Hubs
{
    public class AudioCallHub : Hub
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ChatDbContext _context;

        // Static dictionary to track user connections by username
        private static readonly ConcurrentDictionary<string, string> _userConnections = new();

        private static readonly ConcurrentDictionary<string, string> _activeCalls = new();

        public AudioCallHub(UserManager<AppUser> userManager, ChatDbContext context)
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
                Console.WriteLine($"🎤 Audio: User {userName} connected with ID {Context.ConnectionId}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userName = GetCurrentUserName();
            if (!string.IsNullOrEmpty(userName))
            {
                _userConnections.TryRemove(userName, out _);

                // If user was in a call, notify the other party
                if (_activeCalls.TryGetValue(userName, out var otherUser))
                {
                    _activeCalls.TryRemove(userName, out _);
                    _activeCalls.TryRemove(otherUser, out _);

                    if (_userConnections.TryGetValue(otherUser, out var otherConnectionId))
                    {
                        await Clients.Client(otherConnectionId).SendAsync("CallEnded", userName);
                    }
                }

                Console.WriteLine($"🎤 Audio: User {userName} disconnected");
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
            Console.WriteLine($"🎤 SendOffer: From {fromUser} to {toUser}");

            // Check if caller is already in a call
            if (_activeCalls.ContainsKey(fromUser))
            {
                await Clients.Caller.SendAsync("CallFailed", "You are already in a call");
                return;
            }

            // Check if recipient exists
            if (!_userConnections.TryGetValue(toUser, out var connectionId))
            {
                Console.WriteLine($"❌ User {toUser} not found in connections");
                await Clients.Caller.SendAsync("CallFailed", "User is not available");
                return;
            }

            // Check if recipient is already in a call
            if (_activeCalls.ContainsKey(toUser))
            {
                Console.WriteLine($"❌ User {toUser} is busy");
                // CHANGE THIS LINE - use CallFailed instead of letting it go through
                await Clients.Caller.SendAsync("CallFailed", "User is busy on another call");
                return;
            }

            // Mark both users as in a call
            _activeCalls[fromUser] = toUser;
            _activeCalls[toUser] = fromUser;

            await Clients.Client(connectionId).SendAsync("ReceiveOffer", fromUser, offer);
            Console.WriteLine($"✅ Offer sent to {toUser} at connection {connectionId}");
        }

        public async Task SendIncomingAudioCall(string toUser)
        {
            var fromUser = GetCurrentUserName();
            Console.WriteLine($"🎤 SendIncomingAudioCall: From {fromUser} to {toUser}");

            if (_userConnections.TryGetValue(toUser, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync("IncomingAudioCall", fromUser);
                Console.WriteLine($"✅ Incoming call notification sent to {toUser}");
            }
            else
            {
                Console.WriteLine($"❌ User {toUser} not found for incoming call");
                await Clients.Caller.SendAsync("CallFailed", "User is not available");
            }
        }

        public async Task SendAnswer(string toUser, string answer)
        {
            var fromUser = GetCurrentUserName();
            Console.WriteLine($"🎤 SendAnswer: From {fromUser} to {toUser}");

            // Verify both users are in the same call
            if (!_activeCalls.TryGetValue(fromUser, out var expectedUser) || expectedUser != toUser)
            {
                Console.WriteLine($"❌ Invalid call state for answer from {fromUser} to {toUser}");
                return;
            }

            if (_userConnections.TryGetValue(toUser, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync("ReceiveAnswer", fromUser, answer);
                Console.WriteLine($"✅ Answer sent to {toUser}");
            }
            else
            {
                Console.WriteLine($"❌ Failed to send answer to {toUser}");
            }
        }

        public async Task SendIceCandidate(string toUser, string candidate)
        {
            var fromUser = GetCurrentUserName();
            Console.WriteLine($"🎤 SendIceCandidate: From {fromUser} to {toUser}");

            // Verify both users are in the same call
            if (!_activeCalls.TryGetValue(fromUser, out var expectedUser) || expectedUser != toUser)
            {
                Console.WriteLine($"❌ Invalid call state for ICE candidate from {fromUser} to {toUser}");
                return;
            }

            if (_userConnections.TryGetValue(toUser, out var connectionId))
            {
                var candidateObj = System.Text.Json.JsonSerializer.Deserialize<object>(candidate);
                await Clients.Client(connectionId).SendAsync("ReceiveCandidate", fromUser, candidateObj);
            }
            else
            {
                Console.WriteLine($"❌ Failed to send ICE candidate to {toUser}");
            }
        }

        public async Task EndCall(string toUser)
        {
            var fromUser = GetCurrentUserName();
            Console.WriteLine($"🎤 EndCall: From {fromUser} to {toUser}");

            // Remove call state for both users
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
            Console.WriteLine($"🎤 DeclineCall: {toUser} declining call from {fromUser}");

            // Remove call state for both users
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