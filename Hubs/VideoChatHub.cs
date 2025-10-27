using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RealtimeChat.Context;
using RealtimeChat.Dtos;
using RealtimeChat.Models;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace RealtimeChat.Hubs
{
    public class VideoChatHub : Hub
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ChatDbContext _context;

        public VideoChatHub(UserManager<AppUser> userManager, ChatDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        private string GetUserName()
        {
            return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }

        public async Task SendOffer(string toUser, string offer)
        {
            var fromUser = GetUserName();
            await Clients.User(toUser).SendAsync("ReceiveOffer", fromUser, offer);
        }

        public async Task SendAnswer(string toUser, string answer)
        {
            var fromUser = GetUserName();
            await Clients.User(toUser).SendAsync("ReceiveAnswer", fromUser, answer);
        }

        public async Task SendIceCandidate(string toUser, string candidate)
        {
            var fromUser = GetUserName();
            await Clients.User(toUser).SendAsync("ReceiveCandidate", fromUser, candidate);
        }

        public async Task EndCall(string toUser)
        {
            var fromUser = GetUserName();
            await Clients.User(toUser).SendAsync("CallEnded", fromUser);
            await Clients.Caller.SendAsync("CallEnded", toUser);
        }
    }
}
