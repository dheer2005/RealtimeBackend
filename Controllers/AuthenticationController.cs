using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using RealtimeChat.Context;
using RealtimeChat.Dtos;
using RealtimeChat.Hubs;
using RealtimeChat.Interfaces;
using RealtimeChat.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UAParser;

namespace RealtimeChat.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthenticationController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ChatDbContext _context;
        private readonly IImageService _imageService;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IMemoryCache _cache;

        public AuthenticationController(UserManager<AppUser> userManager, IConfiguration configuration, ChatDbContext context, IImageService imageService, IHubContext<ChatHub> hubContext, IMemoryCache cache)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
            _imageService = imageService;
            _hubContext = hubContext;
            _cache = cache;
        }

        private string GetCurrentUserId()
        {
            return _userManager.GetUserId(User);
        }

        private string GetCurrentUserName()
        {
            return _userManager.GetUserName(User);
        }

        private string GetPublicIdFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            var parts = url.Split('/');
            var folderAndFile = string.Join('/', parts.SkipWhile(p => !p.Contains("Chatlify")));
            return folderAndFile.Replace(".jpg", "").Replace(".png", "").Replace(".jpeg", "");
        }

        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> registerUser([FromForm] RegisterModel model)
        {

            var emailExists = await _userManager.FindByEmailAsync(model.Email);
            var nameExists = await _userManager.FindByNameAsync(model.UserName);

            if (emailExists != null)
            {
                return BadRequest(new { message = "Email already exist" });
            }
            if (nameExists != null)
            {
                return BadRequest(new {message = "UserName is already exist" });
            }

            string profileUrl = null;

            if(model.ProfileImage != null)
            {
                profileUrl = await _imageService.UploadImageAsync(model.ProfileImage);
            }

            var user = new AppUser
            {
                UserName = model.UserName,
                FullName = model.FullName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                ProfileImage = profileUrl,
                CreatedAt = DateTime.Now,
                SecurityStamp = Guid.NewGuid().ToString()
            };
            var result =await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded) 
            {
                return BadRequest(new { message = "User creation failed, Please check user details and try again" });
            }

            return Ok(user);
        }


        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> LoginUser([FromBody] LoginModel model)
        {
            var user = await _userManager.FindByNameAsync(model.UserName);

            if(user != null && await _userManager.CheckPasswordAsync(user, model.Password))
            {
                var jti = Guid.NewGuid().ToString();

                var authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim("UserId", user.Id.ToString()),
                    new Claim(JwtRegisteredClaimNames.Jti, jti),
                    new Claim("UserName", user?.UserName?.ToString()?? ""),
                    new Claim("Email", user?.Email ?? "")
                };

                var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));

                var token = new JwtSecurityToken(
                    issuer: _configuration["JWT:ValidIssuer"],
                    audience: _configuration["JWT:ValidAudience"],
                    claims: authClaims,
                    signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                );

                var userAgent = Request.Headers["User-Agent"].ToString();
                var uaParser = Parser.GetDefault();
                var clientInfo = uaParser.Parse(userAgent);

                var deviceInfo = $"{clientInfo.OS.Family} {clientInfo.OS.Major} - {clientInfo.UA.Family} {clientInfo.UA.Major}";
                var ipAddress = model.ClientIp;

                var existingSession = await _context.UserSessions
                    .FirstOrDefaultAsync(s => s.UserId == user.Id
                        && s.DeviceInfo == deviceInfo && s.IpAddress == ipAddress
                        && !s.IsRevoked);

                if (existingSession == null)
                {
                    var session = new UserSession
                    {
                        UserId = user.Id,
                        JwtId = jti,
                        ExpiresAt = token.ValidTo,
                        CreatedAt = DateTime.UtcNow.AddHours(5.5),
                        DeviceInfo = deviceInfo,
                        IpAddress = ipAddress,
                        IsRevoked = false
                    };

                    _context.UserSessions.Add(session);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    existingSession.ExpiresAt = token.ValidTo;
                    existingSession.CreatedAt = DateTime.UtcNow.AddHours(5.5);
                    existingSession.IpAddress = ipAddress;
                    await _context.SaveChangesAsync();
                }

                await _hubContext.Clients.User(user.Id).SendAsync("SessionChanged", user.Id);

                return Ok(new
                {
                    token = new JwtSecurityTokenHandler().WriteToken(token),
                    expiration = token.ValidTo
                });
            }
            return Unauthorized();
        }

        [Authorize]
        [HttpGet]
        [Route("GetAllUsers/{loggedUserId}")]
        public async Task<IActionResult> GetAllUsers(string loggedUserId)
        {
            var currentUserID = GetCurrentUserId();
            if (currentUserID != loggedUserId)
                return Forbid("Access denied.");

            var users = await _userManager.Users
                .Where(m=>m.UserName != loggedUserId)
                .ToListAsync();

            return Ok(users);
        }

        [Authorize]
        [HttpGet("get-user-info-by-userName/{userName}")]
        public async Task<IActionResult> GetUserInfoByUserName(string userName)
        {
            var currentUserName = GetCurrentUserName();
            var currentUserId = GetCurrentUserId();

            var cacheKey = $"userinfo_{userName}";

            if (!_cache.TryGetValue(cacheKey, out AppUser user))
            {
                user = await _userManager.FindByNameAsync(userName);
                if (user == null)
                    return BadRequest(new { message = "User not found" });

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(15))
                    .SetAbsoluteExpiration(TimeSpan.FromHours(2));

                _cache.Set(cacheKey, user, cacheOptions);
            }

            if (!string.IsNullOrEmpty(currentUserName) && currentUserName.Equals(userName, StringComparison.OrdinalIgnoreCase))
            {
                var infoSelf = new UserInfoDto
                {
                    UserId = user.Id,
                    ProfileImage = user.ProfileImage,
                    UserName = user.UserName,
                    FullName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    CreatedAt = user.CreatedAt
                };

                return Ok(infoSelf);
            }

            if (string.IsNullOrEmpty(currentUserId))
                return Forbid("Access denied.");

            var isFriend = await _context.FriendRequests.AnyAsync(r =>
                ((r.FromUserId == currentUserId && r.ToUserId == user.Id) ||
                 (r.FromUserId == user.Id && r.ToUserId == currentUserId)) &&
                 r.Status == "Accepted"
            );

            if (!isFriend)
                return Forbid("Access denied. You can only view profiles of your friends.");

            var info = new UserInfoDto
            {
                UserId = user.Id,
                ProfileImage = user.ProfileImage,
                UserName = user.UserName,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                CreatedAt = user.CreatedAt
            };

            return Ok(info);
        }

        [Authorize]
        [HttpPut("edit-user-profile/{userId}")]
        public async Task<IActionResult> EditUserProfile(string userId, [FromBody] EditProfileModel model)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId != userId)
                return Forbid("Access denied.");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new { message = "User not found" });

            // Update only editable fields
            user.FullName = model.FullName ?? user.FullName;
            user.Email = model.Email ?? user.Email;
            user.PhoneNumber = model.PhoneNumber ?? user.PhoneNumber;
            user.UserName = model.UserName ?? user.UserName;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(new { message = "Profile update failed", errors = result.Errors });

            _cache.Remove($"userinfo_{user.UserName}");

            return Ok(new { message = "Profile updated successfully", user });
        }

        [Authorize]
        [HttpPut("edit-user-profile-pic/{userId}")]
        public async Task<IActionResult> EditUserProfilePic(string userId, [FromForm] EditProfilePicDto modal)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new { message = "User not found" });

            if (modal.NewProfileImage == null)
                return BadRequest(new { message = "Invalid image file" });

            var newImageUrl = await _imageService.UploadImageAsync(modal.NewProfileImage);

            var publicId = GetPublicIdFromUrl(user.ProfileImage);
            await _imageService.DeleteImageAsync(publicId, "image");

            user.ProfileImage = newImageUrl;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(new { message = "Profile picture update failed", errors = result.Errors });

            _cache.Remove($"userinfo_{user.UserName}");

            return Ok(new { message = "Profile picture updated successfully", profileImage = newImageUrl });
        }
    }
}
