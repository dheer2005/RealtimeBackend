using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealtimeChat.Context;
using RealtimeChat.Models;
using RealtimeChat.Services;

namespace RealtimeChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly ChatDbContext _context;

        public ChatController(ChatDbContext context, IMemoryCache memoryCache)
        {
            _context = context;
        }


        [HttpGet("unread-counts/{fromUser}/{userTo}")]
        public async Task<IActionResult> GetUnreadCount(string fromUser, string userTo)
        {
            if (string.IsNullOrEmpty(fromUser) || string.IsNullOrEmpty(userTo))
                return BadRequest("Invalid usernames.");

            var lastMessage = await _context.Messages
                .Where(m => (m.FromUser == fromUser && m.UserTo == userTo) || (m.FromUser == userTo && m.UserTo == fromUser))
                .OrderByDescending(m => m.Created)
                .FirstOrDefaultAsync();

            var count = await _context.Messages
                .Where(m => m.FromUser == fromUser && m.UserTo == userTo && m.Status != "seen")
                .CountAsync();

            string lastMsgText = string.Empty;

            if(lastMessage != null)
            {
                if (lastMessage.IsImage && !string.IsNullOrEmpty(lastMessage.MediaUrl))
                {
                    if (lastMessage.MediaUrl.Contains("staticmap.openstreetmap.de"))
                        lastMsgText = "📍 Location";
                    else
                        lastMsgText = "📷 Photo";
                }
                else
                {
                    lastMsgText = EncryptionHelper.Decrypt(lastMessage.Message);
                }
            }

            return Ok(new 
            { 
                lastMsgSender = lastMessage?.FromUser, 
                lastMsg = lastMsgText, 
                lastMsgTime = lastMessage?.Created, 
                Count = count 
            });
        }



        [HttpGet("{From}/{user}")]
        public async Task<IActionResult> GetMessages(string From, string user)
        {
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