using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealtimeChat.Context;
using RealtimeChat.Models;

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

            return Ok(new { lastMsgSender = lastMessage?.FromUser, lastMsg =  lastMessage?.Message, lastMsgTime = lastMessage?.Created , Count = count });
        }

        [HttpGet("{From}/{user}")]
        public async Task<IActionResult> GetMessages(string From, string user)
        {
            var list = await _context.Messages.Where(e => e.FromUser == From && e.UserTo == user).OrderBy(e => e.Created).ToListAsync();

            if (From != user)
            {
                var list1 = await _context.Messages.Where(e => e.FromUser == user && e.UserTo == From).OrderBy(e => e.Created).ToListAsync();
                list.AddRange(list1);
            }

            return Ok(list);
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