using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealtimeChat.Context;
using RealtimeChat.Models;
using RealtimeChat.Services;
using System.Text.RegularExpressions;

namespace RealtimeChat.Controllers
{
    
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly ChatDbContext _context;
        private readonly IMemoryCache _cache;

        public ChatController(ChatDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet("unread-summary/{currentUser}")]
        public async Task<IActionResult> GetUnreadSummary(string currentUser)
        {
            var loggedInUser = User.Identity?.Name;

            if (loggedInUser != currentUser)
                return Unauthorized("Invalid token.");

            var cacheKey = $"unread_summary_{currentUser}";

            if (!_cache.TryGetValue(cacheKey, out object grouped))
            {
                var messages = await _context.Messages
                    .Where(m => m.UserTo == currentUser || m.FromUser == currentUser)
                    .OrderByDescending(m => m.Created)
                    .ToListAsync();

                grouped = messages
                    .GroupBy(m => m.FromUser == currentUser ? m.UserTo : m.FromUser)
                    .Select(g =>
                    {
                        var lastMsg = g.First();
                        string displayMessage = "";

                        if (!lastMsg.IsImage)
                        {
                            displayMessage = EncryptionHelper.Decrypt(lastMsg.Message);
                        }
                        else
                        {
                            var decryptedUrl = lastMsg.MediaUrl.Contains("base64") || lastMsg.MediaUrl.Contains("http")
                                ? lastMsg.MediaUrl
                                : EncryptionHelper.Decrypt(lastMsg.MediaUrl);

                            if (decryptedUrl.Contains("staticmap.openstreetmap.de"))
                                displayMessage = "📍 Location";
                            else if (Regex.IsMatch(decryptedUrl, @"\.(jpg|jpeg|png|gif|webp)$", RegexOptions.IgnoreCase))
                                displayMessage = "📷 Photo";
                            else if (Regex.IsMatch(decryptedUrl, @"\.(mp4|mov|avi|mkv|webm)$", RegexOptions.IgnoreCase))
                                displayMessage = "🎬 Video";
                            else
                                displayMessage = "📁 File";
                        }

                        return new
                        {
                            userName = g.Key,
                            unreadCount = g.Count(x => x.UserTo == currentUser && x.Status != "seen"),
                            lastMessage = displayMessage,
                            lastMessageTime = lastMsg.Created,
                            lastMessageSender = lastMsg.FromUser
                        };
                    })
                    .ToList();

                // Short cache duration for real-time messaging
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromSeconds(30))
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(2));

                _cache.Set(cacheKey, grouped, cacheOptions);
            }

            return Ok(grouped);
        }


        [HttpGet("{user}")]
        public async Task<IActionResult> GetMessages(string user)
        {
            var loggedInUser = User.Identity?.Name;

            if (string.IsNullOrEmpty(loggedInUser))
                return Unauthorized("Invalid or expired token.");

            if (string.IsNullOrEmpty(user))
                return BadRequest("Invalid target username.");

            var From = loggedInUser;

            var messages = await _context.Messages
                .Where(m => (m.FromUser == From && m.UserTo == user) || (m.FromUser == user && m.UserTo == From))
                .OrderBy(m => m.Created)
                .Select(m => new
                {
                    id = m.Id,
                    fromUser = m.FromUser,
                    userTo = m.UserTo,
                    message = m.Message,
                    created = m.Created,
                    status = m.Status,
                    isImage = m.IsImage,
                    mediaUrl = m.MediaUrl,
                    reactions = m.Reactions,
                    replyTo = m.ReplyToMessageId.HasValue
                        ? _context.Messages
                            .Where(r => r.Id == m.ReplyToMessageId.Value)
                            .Select(r => new
                            {
                                id = r.Id,
                                message = r.Message,
                                mediaUrl = r.MediaUrl,
                                isImage = r.IsImage
                            })
                            .FirstOrDefault()
                        : null
                })
                .ToListAsync();

            var decryptedMessages = messages.Select(m => new
            {
                m.id,
                m.fromUser,
                m.userTo,
                message = EncryptionHelper.Decrypt(m.message),
                m.created,
                m.status,
                m.isImage,
                mediaUrl = m.isImage && m.mediaUrl != null && !m.mediaUrl.Contains("staticmap.openstreetmap.de")
                    ? EncryptionHelper.Decrypt(m.mediaUrl)
                    : m.mediaUrl,
                m.reactions,
                replyTo = m.replyTo != null ? new
                {
                    m.replyTo.id,
                    message = EncryptionHelper.Decrypt(m.replyTo.message),
                    mediaUrl = m.replyTo.isImage && m.replyTo.mediaUrl != null && !m.replyTo.mediaUrl.Contains("staticmap.openstreetmap.de")
                        ? EncryptionHelper.Decrypt(m.replyTo.mediaUrl)
                        : m.replyTo.mediaUrl,
                    m.replyTo.isImage
                } : null
            });

            return Ok(decryptedMessages);
        }

        [HttpPost("groupChat")]
        public async Task<IActionResult> SaveGroupChats([FromBody] Group_chats group)
        {
            group.Created = DateTime.UtcNow.AddHours(5.5);
            _context.GroupChats.Add(group);
            await _context.SaveChangesAsync();
            return Ok(group);
        }


        [HttpGet("getGroupMessages/{groupName}")]
        public async Task<IActionResult> GetGroupMessages(string groupName)
        {
            var messages = await _context.GroupChats
                .Where(g => g.GroupName == groupName)
                .OrderBy(g => g.Created)
                .ToListAsync();

            return Ok(messages);
        }



    }

}