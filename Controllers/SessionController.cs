using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RealtimeChat.Context;
using RealtimeChat.Hubs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace RealtimeChat.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SessionController : ControllerBase
    {
        private readonly ChatDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;
        public SessionController(ChatDbContext context, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }


        [HttpGet("active-session")]
        public async Task<IActionResult> GetAllActiveSessions()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var sessions = await _context.UserSessions
                .Where(s=>s.UserId == userId && !s.IsRevoked)
                .Select(s=>new
                {
                    s.Id,
                    s.DeviceInfo,
                    s.IpAddress,
                    s.JwtId,
                    s.CreatedAt,
                    s.ExpiresAt
                })
                .ToListAsync();

            return Ok(sessions);
        }

        [HttpPost("logout/current")]
        public async Task<IActionResult> LogoutCurrent()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
            var session = await _context.UserSessions.FirstOrDefaultAsync(us=>us.JwtId == jti);

            if(session != null)
            {
                //session.IsRevoked = true;
                _context.UserSessions.Remove(session);
                await _context.SaveChangesAsync();
                await _hubContext.Clients.Group(session.JwtId).SendAsync("ForceLogout");
                await _hubContext.Clients.User(userId).SendAsync("SessionChanged", userId);
            }

            return Ok(new { message = "Logged out from current device." });
        }

        [HttpPost("logout/all")]
        public async Task<IActionResult> LogoutAll()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentJwtId = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
            var sessions = await _context.UserSessions.Where(s=> s.UserId == userId && !s.IsRevoked && s.JwtId != currentJwtId).ToListAsync();

            foreach (var session in sessions) 
            {

                //session.IsRevoked = true;
                await _hubContext.Clients.Group(session.JwtId).SendAsync("ForceLogout");
                await _hubContext.Clients.User(userId).SendAsync("SessionChanged", userId);
            }

            _context.UserSessions.RemoveRange(sessions);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Logged out from all devices." });
        }

        [HttpPost("logout-device/{sessionId}")]
        public async Task<IActionResult> LogoutSelectedDevice(int sessionId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var session = await _context.UserSessions.FirstOrDefaultAsync(s=>s.Id == sessionId && s.UserId == userId);

            if (session == null) return BadRequest(new { message = "SessionId not found" });
            
            //session.IsRevoked = true;
            _context.UserSessions.Remove(session);

            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group(session.JwtId).SendAsync("ForceLogout");

            await _hubContext.Clients.User(userId).SendAsync("SessionChanged", userId);

            return Ok(new { message = "Selected device logged out successfully." });
        }
    }
}
