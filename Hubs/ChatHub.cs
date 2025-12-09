using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealtimeChat.Context;
using RealtimeChat.Dtos;
using RealtimeChat.Models;
using RealtimeChat.Services;
using System.Security.Claims;
using System.Text.Json;

namespace RealtimeChat.Hubs
{
    public class ChatHub : Hub
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ChatDbContext _context;
        private readonly IMemoryCache _cache;
        private static readonly Dictionary<string, OnlineUserDto> onlineUsers = new Dictionary<string, OnlineUserDto>();

        public ChatHub(UserManager<AppUser> userManager, ChatDbContext context, IMemoryCache cache)
        {
            _userManager = userManager;
            _context = context;
            _cache = cache;
        }

        public override async Task OnConnectedAsync()
        {
            var userName = GetUserName();
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var connectionId = Context.ConnectionId;

            if (!string.IsNullOrEmpty(userName))
            {
                if (onlineUsers.ContainsKey(userName))
                {
                    onlineUsers[userName].ConnectionId = connectionId;
                    onlineUsers[userName].IsOnline = true;
                }
                else
                {
                    var onlineUser = new OnlineUserDto
                    {
                        ConnectionId = connectionId,
                        UserName = userName,
                        IsOnline = true

                    };

                    onlineUsers.TryAdd(userName, onlineUser);
                }

                await Clients.Others.SendAsync("UserOnline", userName);

                await Clients.All.SendAsync("OnlineUsers", await GetAllUsers());
            }

            await base.OnConnectedAsync();
        }

        private string GetUserName()
        {
            return Context.User?.FindFirst(ClaimTypes.Name)?.Value
               ?? Context.User?.FindFirst("unique_name")?.Value
               ?? Context.User?.Identity?.Name
               ?? string.Empty;
        }

        public async Task SendMessage(string fromUser, string userTo, string message, DateTime created, string status, bool isImage, string mediaUrl, int? replyToMessageId)
        {
            if (string.IsNullOrEmpty(fromUser) || string.IsNullOrEmpty(userTo))
                throw new ArgumentException("Invalid message data");

            bool isLocation = isImage && !string.IsNullOrEmpty(mediaUrl) && mediaUrl.Contains("staticmap.openstreetmap.de");

            string encryptedMessage = EncryptionHelper.Encrypt(message);
            string encryptedMediaUrl =(!isLocation && isImage && !string.IsNullOrEmpty(mediaUrl)) 
                ? EncryptionHelper.Encrypt(mediaUrl) 
                : mediaUrl;

            var newMessage = new Messages
            {
                FromUser = fromUser,
                UserTo = userTo,
                Message = encryptedMessage,
                Status = "sent", 
                IsImage = isImage,
                MediaUrl = encryptedMediaUrl,
                Created = DateTime.UtcNow.AddHours(5.5),
                ReplyToMessageId = replyToMessageId,
            };

            _context.Messages.Add(newMessage);
            await _context.SaveChangesAsync();

            _cache.Remove($"unread_summary_{fromUser}");
            _cache.Remove($"unread_summary_{userTo}");

            Messages? repliedMessage = null;
            if (replyToMessageId.HasValue)
            {
                repliedMessage = await _context.Messages
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m=>m.Id == replyToMessageId.Value);
            }

            var serverMessage = new
            {
                id = newMessage.Id,
                fromUser = newMessage.FromUser,
                userTo = newMessage.UserTo,
                message = EncryptionHelper.Decrypt(newMessage.Message),
                created = newMessage.Created,
                status = newMessage.Status,
                isImage = newMessage.IsImage,
                mediaUrl = newMessage.IsImage
                    ? (isLocation ? newMessage.MediaUrl : EncryptionHelper.Decrypt(newMessage.MediaUrl ?? ""))
                    : null,
                replyTo = repliedMessage != null ? new
                {
                    id = repliedMessage.Id,
                    message = EncryptionHelper.Decrypt(repliedMessage.Message),
                    mediaUrl = repliedMessage.IsImage
                        ? (repliedMessage.MediaUrl != null && repliedMessage.MediaUrl.Contains("staticmap.openstreetmap.de")
                            ? repliedMessage.MediaUrl
                            : EncryptionHelper.Decrypt(repliedMessage.MediaUrl ?? ""))
                        : null,
                    isImage = repliedMessage.IsImage
                } : null
            };

            var recipientConnectionId = onlineUsers.TryGetValue(userTo, out var recipientUser) && recipientUser != null
                ? recipientUser.ConnectionId
                : null;

            if (recipientConnectionId != null)
            {
                await Clients.Client(recipientConnectionId).SendAsync("ReceiveMessage", serverMessage);
            }

            await Clients.Caller.SendAsync("ReceiveMessage", serverMessage);
        }

        public async Task DeleteMessage(int messageId)
        {
            var message = await _context.Messages.FindAsync(messageId);

            if (message == null)
                throw new Exception("Message not found");

            var fromUser = message.FromUser;
            var userTo = message.UserTo;

            _context.Messages.Remove(message);
            await _context.SaveChangesAsync();

            _cache.Remove($"unread_summary_{fromUser}");
            _cache.Remove($"unread_summary_{userTo}");

            var senderConnectionId = onlineUsers.Values.FirstOrDefault(u => u.UserName == fromUser)?.ConnectionId;
            var receiverId = onlineUsers.Values.FirstOrDefault(u => u.UserName == userTo)?.ConnectionId;

            if (senderConnectionId != null)
            {
                await Clients.Client(senderConnectionId).SendAsync("MessageDeleted", messageId);
            }

            if (receiverId != null) 
            {
                await Clients.Client(receiverId).SendAsync("MessageDeleted", messageId);
            }

        }

        public async Task NotifyTyping(string recipientUserName)
        {
            var senderUserName = GetUserName();

            if (string.IsNullOrEmpty(senderUserName) || string.IsNullOrEmpty(recipientUserName))
            {
                return;
            }

            var connectionId = onlineUsers.TryGetValue(recipientUserName, out var user) && user != null
                ? user.ConnectionId
                : null;

            if (connectionId != null)
            {
                await Clients.Client(connectionId).SendAsync("UserTyping", senderUserName);
            }
        }

        public async Task NotifyStopTyping(string recipientUserName)
        {
            var senderUserName = GetUserName();

            if (string.IsNullOrEmpty(senderUserName) || string.IsNullOrEmpty(recipientUserName))
            {
                return;
            }

            var connectionId = onlineUsers.TryGetValue(recipientUserName, out var user) && user != null
                ? user.ConnectionId
                : null;

            if (connectionId != null)
            {
                await Clients.Client(connectionId).SendAsync("UserStopTyping", senderUserName);
            }
        }

        public async Task MarkAsSeen(string fromUser, string userTo)
        {
            var currentUser = GetUserName();

            var messages = await _context.Messages
                .Where(m => m.FromUser == fromUser && m.UserTo == currentUser && m.Status != "seen")
                .ToListAsync();

            if (messages.Any())
            {
                foreach (var msg in messages)
                {
                    msg.Status = "seen";
                }

                await _context.SaveChangesAsync();

                _cache.Remove($"unread_summary_{currentUser}");
                _cache.Remove($"unread_summary_{fromUser}");

                var senderConnectionId = onlineUsers.TryGetValue(fromUser, out var senderUser) && senderUser != null
                    ? senderUser.ConnectionId
                    : null;

                if (senderConnectionId != null)
                {
                    await Clients.Client(senderConnectionId).SendAsync("MessagesSeen", currentUser);
                }

                await Clients.Caller.SendAsync("MessagesMarkedAsSeen", fromUser);
            }
        }

        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task SendMessageToGroup(string groupName, string fromUser, string message, DateTime created)
        {
            await Clients.Group(groupName).SendAsync("ReceiveGroupMessage", groupName, fromUser, message, created);
        }

        private async Task<IEnumerable<OnlineUserDto>> GetAllUsers()
        {
            var userName = GetUserName();
            var onlineUsersSet = new HashSet<string>(onlineUsers.Where(u =>u.Value != null &&  u.Value.IsOnline).Select(u => u.Key));

            var users = await _userManager.Users
                .Where(u=>u.UserName != null)
                .Select(u => new OnlineUserDto
            {
                Id = u.Id,
                UserName = u.UserName ?? "",
                IsOnline = onlineUsersSet.Contains(u.UserName!),
                FullName = u.FullName ?? "",
                Email = u.Email ?? "",
                PhoneNumber = u.PhoneNumber ?? "",
                ProfileImage = u.ProfileImage ?? "",
            })
            .OrderByDescending(u => u.IsOnline)
                .ThenBy(u => u.UserName)
            .ToListAsync();

            return users;
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userName = GetUserName();
            var connectionId = Context.ConnectionId;

            if (!string.IsNullOrEmpty(userName))
            {
                if (onlineUsers.Remove(userName, out var removedUser))
                {
                    await Clients.Others.SendAsync("UserOffline", userName);

                    await Clients.All.SendAsync("OnlineUsers", await GetAllUsers());
                }
            }

            if (exception != null)
            {
                Console.WriteLine($"Connection closed with exception: {exception.Message}");
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task AddReaction(int messageId, string emoji, string reactingUser)
        {
            var message = await _context.Messages.FindAsync(messageId);

            if (message != null)
            {
                var reactions = new List<Reaction>();
                if (!string.IsNullOrEmpty(message.Reactions))
                {
                    reactions = System.Text.Json.JsonSerializer.Deserialize<List<Reaction>>(message.Reactions);
                }

                var existing = reactions.FirstOrDefault(r => r.User == reactingUser);
                if (existing != null)
                {
                    reactions.Remove(existing);
                }

                reactions.Add(new Reaction 
                { 
                    User = reactingUser, 
                    Emoji = emoji 
                });
                message.Reactions = System.Text.Json.JsonSerializer.Serialize(reactions);
                await _context.SaveChangesAsync();

                await Clients.All.SendAsync("ReactionAdded", messageId, emoji, reactingUser);
            }
        }

        public async Task RemoveReaction(int messageId, string reactingUser)
        {
            var message = await _context.Messages.FindAsync(messageId);
            if (message != null)
            {
                var reactions = new List<Reaction>();
                if (!string.IsNullOrEmpty(message.Reactions))
                {
                    reactions = JsonSerializer.Deserialize<List<Reaction>>(message.Reactions) ?? new List<Reaction>();
                }

                var existing = reactions.FirstOrDefault(r => r.User == reactingUser);
                if (existing != null)
                {
                    reactions.Remove(existing);
                }

                message.Reactions = System.Text.Json.JsonSerializer.Serialize(reactions);
                await _context.SaveChangesAsync();

                await Clients.All.SendAsync("ReactionRemoved", messageId, reactingUser);
            }
        }

        public async Task SendFriendRequestNotification(string toUserId, object request)
        {
            await Clients.User(toUserId).SendAsync("ReceiveFriendRequest", request);
        }

        public async Task FriendRequestResponseNotification(string fromUserId, string toUserId, object response)
        {
            await Clients.User(fromUserId).SendAsync("FriendRequestResponse", response);
            await Clients.User(toUserId).SendAsync("FriendRequestResponse", response);
        }

        public async Task UnfriendNotification(string user1, string user2)
        {
            await Clients.Users(user1, user2).SendAsync("Unfriended", new { user1, user2 });
        }


        public async Task JoinJwtGroup(string jwtId)
        {
            if (!string.IsNullOrEmpty(jwtId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, jwtId);
            }
        }

        public async Task NotifySessionChanged(string userId)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                await Clients.User(userId).SendAsync("SessionChanged", userId);
            }
        }

        public async Task ForceLogoutDevice(string jwtId)
        {
            if (!string.IsNullOrEmpty(jwtId))
            {
                await Clients.Group(jwtId).SendAsync("ForceLogout");
            }
        }
    }
}