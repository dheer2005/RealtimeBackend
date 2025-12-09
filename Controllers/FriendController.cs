using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealtimeChat.Context;
using RealtimeChat.Dtos;
using RealtimeChat.Hubs;
using RealtimeChat.Models;

namespace RealtimeChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FriendController : ControllerBase
    {
        private readonly ChatDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IHubContext<ChatHub> _hub;
        private readonly IMemoryCache _cache;
        public FriendController(UserManager<AppUser> userManager, ChatDbContext context, IHubContext<ChatHub> hub, IMemoryCache cache)
        {
            _context = context;
            _userManager = userManager;
            _hub = hub;
            _cache = cache;
        }

        private string GetCurrentUserId()
        {
            return _userManager.GetUserId(User);
        }

        private void InvalidateFriendCache(string userId1, string userId2)
        {
            _cache.Remove($"friends_{userId1}");
            _cache.Remove($"friends_{userId2}");
        }

        [HttpPost("send-request")]
        public async Task<IActionResult> SendRequest([FromBody] SendFriendRequestDto request)
        {
            var currentUserId = GetCurrentUserId();
            if (request.FromUserId != currentUserId)
                return Forbid("You cannot send requests as another user.");

            if (request.FromUserId == request.ToUserId)
                return BadRequest(new { message = "You can't send request to yourself" });

            var exist = await _context.FriendRequests
                .AnyAsync(fr => ((fr.FromUserId == request.FromUserId && fr.ToUserId == request.ToUserId) ||
                 (fr.FromUserId == request.ToUserId && fr.ToUserId == request.FromUserId)) && fr.Status == "Pending");

            if (exist)
                return BadRequest(new { message = "Request already sent" });

            var newRequest = new FriendRequest
            {
                FromUserId = request.FromUserId,
                ToUserId = request.ToUserId,
                Status = "Pending",
                RequestedAt = DateTime.Now
            };

            _context.FriendRequests.Add(newRequest);
            await _context.SaveChangesAsync();

            await _hub.Clients.User(request.ToUserId).SendAsync("ReceiveFriendRequest", new { newRequest.Id, FromUserId = request.FromUserId, ToUserId = request.ToUserId });

            return Ok(new { message = "Friend request sent.." });
        }

        [HttpPost("friend-request-response")]
        public async Task<IActionResult> ResponseRequest([FromBody] ResponseRequestDto req)
        {
            var currentUserId = GetCurrentUserId();
            var request = await _context.FriendRequests.Include(f=>f.FromUser).Include(f=>f.ToUser).FirstOrDefaultAsync(f => f.Id == req.RequestId);
            if (request == null)
                return NotFound(new { message = "Request not found" });

            if (request.ToUserId != currentUserId && request.FromUserId != currentUserId)
                return Forbid("You are not part of this friend request.");

            if (req.Action == "accept")
            {
                request.Status = "Accepted";
                InvalidateFriendCache(request.FromUserId, request.ToUserId);
            }
            else if (req.Action == "reject")
            {
                request.Status = "Rejected";
            }
            await _context.SaveChangesAsync();

            await _hub.Clients.Users(request.FromUserId, request.ToUserId).SendAsync("FriendRequestResponse", new { fromUserId = request.FromUserId, fromUser = request.FromUser, toUser = request.ToUser, toUserId= request.ToUserId, Status = request.Status });

            return Ok(request);
        }

        [HttpGet("friends/{userId}")]
        public async Task<IActionResult> GetFriends(string userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId != userId)
                return Forbid("Access denied.");

            var cacheKey = $"friends_{userId}";

            if (!_cache.TryGetValue(cacheKey, out List<AppUser> friendsList))
            {
                var friends = await _context.FriendRequests
                    .Where(r => (r.FromUserId == userId || r.ToUserId == userId) && r.Status == "Accepted")
                    .ToListAsync();

                var friendIds = friends
                    .Select(r => r.FromUserId == userId ? r.ToUserId : r.FromUserId)
                    .ToList();

                friendsList = await _userManager.Users
                    .Where(u => friendIds.Contains(u.Id))
                    .ToListAsync();

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                    .SetAbsoluteExpiration(TimeSpan.FromHours(1));

                _cache.Set(cacheKey, friendsList, cacheOptions);
            }

            return Ok(friendsList);
        }

        [HttpGet("requests/{userId}")]
        public async Task<IActionResult> GetPendingRequests(string userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId != userId)
                return Forbid("Access denied.");

            var request = await _context.FriendRequests
                .Where(r=>r.ToUserId == userId && r.Status == "Pending")
                .Include(r => r.FromUser)
                .Include(r => r.ToUser)
                .Select(r=>new
                {
                    id = r.Id,
                    fromUserId = r.FromUserId,
                    toUserId = r.ToUserId,
                    status = r.Status,
                    fromUser = new
                    {
                        id = r.FromUser.Id,
                        userName = r.FromUser.UserName,
                        fulName = r.FromUser.FullName,
                        email = r.FromUser.Email,
                        profileImage = r.FromUser.ProfileImage
                    },
                    toUser = new
                    {
                        id = r.ToUser.Id,
                        userName = r.ToUser.UserName,
                        fulName = r.ToUser.FullName,
                        email = r.ToUser.Email,
                        profileImage = r.ToUser.ProfileImage
                    }
                })
                .ToListAsync();

            return Ok(request);
        }


        [HttpGet("search/{query}/{currentUserId}")]
        public async Task<IActionResult> SearchUsers(string query, string currentUserId)
        {

            var jwtUserId = GetCurrentUserId();
            if (jwtUserId != currentUserId)
                return Forbid("Access denied.");

            var users = await _userManager.Users
                .Where(u => u.UserName.Contains(query) && u.Id != currentUserId)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.UserName,
                    u.Email,
                    u.ProfileImage
                })
                .ToListAsync();

                var friendRequests = await _context.FriendRequests
                    .Where(r => (r.FromUserId == currentUserId || r.ToUserId == currentUserId))
                    .ToListAsync();

                var result = users.Select(u =>
                {
                    var request = friendRequests
                        .Where(r => (r.FromUserId == currentUserId && r.ToUserId == u.Id) ||
                                    (r.FromUserId == u.Id && r.ToUserId == currentUserId))
                        .OrderByDescending(r => r.RequestedAt)
                        .FirstOrDefault();

                    string status = "none";

                    if (request != null)
                    {
                        var reqStatus = request.Status.ToLower();

                        if (reqStatus == "accepted")
                        {
                            status = "friend";
                        }
                        else if (reqStatus == "pending")
                        {
                            if (request.FromUserId == currentUserId)
                            {
                                status = "requested";
                            }
                            else
                            {
                                status = "incoming";
                            }
                        }
                    }

                    return new
                    {
                        u.Id,
                        u.FullName,
                        u.UserName,
                        u.Email,
                        u.ProfileImage,
                        RelationshipStatus = status
                    };
                });

             return Ok(result);
        }


        [HttpDelete("unfriend/{currentUserId}/{friendId}")]
        public async Task<IActionResult> Unfriend(string currentUserId, string friendId)
        {
            var jwtUserId = GetCurrentUserId();
            if (jwtUserId != currentUserId)
                return Forbid("Access denied.");

            var relation = await _context.FriendRequests.Where(r =>
                (r.FromUserId == currentUserId && r.ToUserId == friendId) ||
                (r.FromUserId == friendId && r.ToUserId == currentUserId))
            .OrderByDescending(r=>r.RequestedAt)
            .FirstOrDefaultAsync();

            var requestForCancel = relation.Status == "Pending" ? true : false;

            if (relation == null)
                return NotFound(new { message = "Friendship not found" });

            //remove range of user messages
            var currentUser = await _context.Users.FindAsync(currentUserId);
            var friendUser = await _context.Users.FindAsync(friendId);

            if (currentUser == null || friendUser == null)
                return NotFound(new { message = "User not found" });

            var currentUsername = currentUser.UserName;
            var friendUsername = friendUser.UserName;

            var messagesToDelete = await _context.Messages.Where(m =>
                (m.FromUser == currentUsername && m.UserTo == friendUsername) ||
                (m.FromUser == friendUsername && m.UserTo == currentUsername))
            .ToListAsync();

            _context.Messages.RemoveRange(messagesToDelete);


            _context.FriendRequests.Remove(relation);
            await _context.SaveChangesAsync();

            InvalidateFriendCache(currentUserId, friendId);

            await _hub.Clients.Users(currentUserId, friendId).SendAsync("Unfriended", new { fromUser = currentUserId, toUser = friendId, forCancel = requestForCancel });

            return Ok(new { message = "Unfriended successfully" });
        }

        [HttpGet("sent-requests/{userId}")]
        public async Task<IActionResult> GetSentPendingRequests(string userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId != userId)
                return Forbid("Access denied.");

            var sentRequests = await _context.FriendRequests
                .Where(r => r.FromUserId == userId && r.Status == "Pending")
                .Include(r => r.ToUser)
                .Select(r => new
                {
                    id = r.Id,
                    fromUserId = r.FromUserId,
                    toUserId = r.ToUserId,
                    status = r.Status,
                    toUser = new
                    {
                        id = r.ToUser.Id,
                        userName = r.ToUser.UserName,
                        fullName = r.ToUser.FullName,
                        email = r.ToUser.Email,
                        profileImage = r.ToUser.ProfileImage
                    }
                })
                .ToListAsync();

            return Ok(sentRequests);
        }
    }
}
