using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
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

        public AuthenticationController(UserManager<AppUser> userManager, IConfiguration configuration, ChatDbContext context, IImageService imageService, IHubContext<ChatHub> hubContext)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
            _imageService = imageService;
            _hubContext = hubContext;
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


        [HttpGet]
        [Route("GetAllUsers/{loggedUserId}")]
        public async Task<IActionResult> GetAllUsers(string loggedUserId)
        {
            var users = await _userManager.Users
                .Where(m=>m.UserName != loggedUserId)
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("get-user-info-by-userName/{userName}")]
        public async Task<IActionResult> GetUserInfoByUserName(string userName)
        {
            var user = await _userManager.FindByNameAsync(userName);

            if (user == null)
                return BadRequest(new { message = "User not found" });

            var info = new UserInfoDto
            {
                UserId = user.Id,
                ProfileImage = user.ProfileImage,
                UserName = user.UserName,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber
            };

            return Ok(info);
        }
    }
}
