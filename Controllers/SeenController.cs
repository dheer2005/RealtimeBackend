using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealtimeChat.Context;
using RealtimeChat.Models;

namespace RealtimeChat.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SeenController : Controller
    {

        private readonly ChatDbContext _context;
        public SeenController(ChatDbContext context)
        {
            _context = context;
        }


        [HttpPost]
        [Route("messages/mark-seen")]
        public async Task<IActionResult> MarkSeen([FromBody] SeenUpdate model)
        {
            var messages = await _context.Messages
                .Where(m => m.FromUser == model.ToUser && m.UserTo == model.FromUser && m.Status != "seen")
                .ToListAsync();

            foreach (var msg in messages)
            {
                msg.Status = "seen";
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        


        [HttpGet]
        [Route("messages/unread-counts")]
        public async Task<IActionResult> GetUnreadCounts()
        {
            var result = await _context.Messages
                .Where(m => m.Status == "sent")
                .GroupBy(m => new { m.FromUser, m.UserTo })
                .Select(group => new {
                    FromUser = group.Key.FromUser,
                    UserTo = group.Key.UserTo,
                    UnreadCount = group.Count(),
                    LatestMessageTime = group.Max(m => m.Created)
                })
                .ToListAsync();

            return Ok(result);
        }


        [HttpPost]
        [Route("messages/update-status")]
        public async Task<IActionResult> UpdateStatus([FromBody] StatusUpdate model)
        {
            var msg = await _context.Messages.FindAsync(model.Id);
            if (msg != null)
            {
                msg.Status = model.Status;
                await _context.SaveChangesAsync();
            }
            return Ok();
        }
        
    }
}
