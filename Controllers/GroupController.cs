using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealtimeChat.Context;
using RealtimeChat.Models;
using RealtimeChat.Services;
using static RealtimeChat.Dtos.GroupDtos;

namespace RealtimeChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GroupController : ControllerBase
    {
        private readonly ChatDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public GroupController(ChatDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto dto)
        {
            var currentUser = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentUser))
                return Unauthorized();

            var user = await _userManager.FindByNameAsync(currentUser);
            if (user == null) return NotFound("User not found");

            var group = new ChatGroup
            {
                GroupName = dto.GroupName,
                GroupImage = dto.GroupImage,
                CreatedBy = user.Id,
                CreatedAt = DateTime.UtcNow.AddHours(5.5)
            };

            _context.ChatGroups.Add(group);
            await _context.SaveChangesAsync();

            // Add creator as admin
            var creatorMember = new GroupMember
            {
                GroupId = group.Id,
                UserId = user.Id,
                UserName = user.UserName ?? "",
                IsAdmin = true,
                JoinedAt = DateTime.UtcNow.AddHours(5.5)
            };
            _context.GroupMembers.Add(creatorMember);

            // Add other members
            foreach (var userId in dto.MemberUserIds.Where(id => id != user.Id))
            {
                var memberUser = await _userManager.FindByIdAsync(userId);
                if (memberUser != null)
                {
                    var member = new GroupMember
                    {
                        GroupId = group.Id,
                        UserId = userId,
                        UserName = memberUser.UserName ?? "",
                        IsAdmin = false,
                        JoinedAt = DateTime.UtcNow.AddHours(5.5)
                    };
                    _context.GroupMembers.Add(member);
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { groupId = group.Id, groupName = group.GroupName });
        }

        [HttpGet("my-groups")]
        public async Task<IActionResult> GetMyGroups()
        {
            var currentUser = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentUser))
                return Unauthorized();

            var user = await _userManager.FindByNameAsync(currentUser);
            if (user == null)
                return NotFound();

            var groups = await _context.GroupMembers
                .Where(gm => gm.UserId == user.Id)
                .Select(gm => new
                {
                    gm.Group.Id,
                    gm.Group.GroupName,
                    gm.Group.GroupImage,
                    gm.Group.CreatedBy,
                    gm.Group.CreatedAt,

                    Members = gm.Group.Members.Select(m => new GroupMemberDto
                    {
                        UserId = m.UserId,
                        UserName = m.UserName,
                        IsAdmin = m.IsAdmin,
                        JoinedAt = m.JoinedAt
                    }).ToList(),

                    LastMessage = gm.Group.Messages
                        .OrderByDescending(m => m.Created)
                        .Select(m => new
                        {
                            m.Id,
                            m.GroupId,
                            m.FromUser,
                            m.Message,
                            m.IsImage,
                            m.MediaUrl,
                            m.Created,
                            m.Status
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();

            var finalGroups = groups
                .Select(g => new GroupDetailsDto
                {
                    Id = g.Id,
                    GroupName = g.GroupName,
                    GroupImage = g.GroupImage,
                    CreatedBy = g.CreatedBy,
                    CreatedAt = g.CreatedAt,
                    Members = g.Members,

                    LastMessage = g.LastMessage == null ? null : new GroupMessageDto
                    {
                        Id = g.LastMessage.Id,
                        GroupId = g.LastMessage.GroupId,
                        FromUser = g.LastMessage.FromUser,
                        Message = EncryptionHelper.Decrypt(g.LastMessage.Message),
                        IsImage = g.LastMessage.IsImage,
                        MediaUrl = g.LastMessage.MediaUrl,
                        Created = g.LastMessage.Created,
                        Status = g.LastMessage.Status
                    },

                    UnreadCount = 0
                })
                .OrderByDescending(g =>
                    g.LastMessage != null
                        ? g.LastMessage.Created
                        : g.CreatedAt)
                .ToList();

            return Ok(finalGroups);
        }

        [HttpGet("{groupId}/messages")]
        public async Task<IActionResult> GetGroupMessages(int groupId)
        {
            var currentUser = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentUser))
                return Unauthorized();

            var user = await _userManager.FindByNameAsync(currentUser);
            if (user == null) return NotFound();

            var isMember = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == user.Id);

            if (!isMember)
                return Forbid("You are not a member of this group");

            var messages = await _context.GroupMessages
                .Where(m => m.GroupId == groupId)
                .OrderBy(m => m.Created)
                .Select(m => new GroupMessageDto
                {
                    Id = m.Id,
                    GroupId = m.GroupId,
                    FromUser = m.FromUser,
                    Message = m.Message,
                    IsImage = m.IsImage,
                    MediaUrl = m.MediaUrl,
                    Created = m.Created,
                    Status = m.Status,
                    ReplyToMessageId = m.ReplyToMessageId,
                    ReplyTo = m.ReplyToMessageId.HasValue
                        ? _context.GroupMessages
                            .Where(r => r.Id == m.ReplyToMessageId.Value)
                            .Select(r => new GroupMessageDto
                            {
                                Id = r.Id,
                                Message = r.Message,
                                MediaUrl = r.MediaUrl,
                                IsImage = r.IsImage,
                                FromUser = r.FromUser
                            })
                            .FirstOrDefault()
                        : null
                })
                .ToListAsync();

            var decryptedMessages = messages.Select(m => new GroupMessageDto
            {
                Id = m.Id,
                GroupId = m.GroupId,
                FromUser = m.FromUser,
                Message = EncryptionHelper.Decrypt(m.Message),
                IsImage = m.IsImage,
                MediaUrl = m.MediaUrl,
                Created = m.Created,
                Status = m.Status,
                ReplyToMessageId = m.ReplyToMessageId,
                ReplyTo = m.ReplyTo != null ? new GroupMessageDto
                {
                    Id = m.ReplyTo.Id,
                    Message = EncryptionHelper.Decrypt(m.ReplyTo.Message),
                    MediaUrl = m.ReplyTo.MediaUrl,
                    IsImage = m.ReplyTo.IsImage,
                    FromUser = m.ReplyTo.FromUser
                } : null
            }).ToList();

            return Ok(decryptedMessages);
        }

        [HttpPut("update")]
        public async Task<IActionResult> UpdateGroup([FromBody] UpdateGroupDto dto)
        {
            var currentUser = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentUser))
                return Unauthorized();

            var user = await _userManager.FindByNameAsync(currentUser);
            if (user == null) return NotFound();

            var isAdmin = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == dto.GroupId && gm.UserId == user.Id && gm.IsAdmin);

            if (!isAdmin)
                return Forbid("Only admins can update group details");

            var group = await _context.ChatGroups.FindAsync(dto.GroupId);
            if (group == null)
                return NotFound("Group not found");

            if (!string.IsNullOrEmpty(dto.GroupName))
                group.GroupName = dto.GroupName;

            if (dto.GroupImage != null)
                group.GroupImage = dto.GroupImage;

            group.UpdatedAt = DateTime.UtcNow.AddHours(5.5);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Group updated successfully" });
        }

        [HttpPost("add-members")]
        public async Task<IActionResult> AddMembers([FromBody] AddMembersDto dto)
        {
            var currentUser = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentUser))
                return Unauthorized();

            var user = await _userManager.FindByNameAsync(currentUser);
            if (user == null) return NotFound();

            var isAdmin = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == dto.GroupId && gm.UserId == user.Id && gm.IsAdmin);

            if (!isAdmin)
                return Forbid("Only admins can add members");

            var uniqueUserIds = dto.UserIds.Distinct().ToList();
            var existingUserIds = await _context.GroupMembers
                .Where(gm => gm.GroupId == dto.GroupId)
                .Select(gm => gm.UserId)
                .ToListAsync();

            var addedMembers = new List<GroupMemberDto>();

            foreach (var userId in uniqueUserIds.Except(existingUserIds))
            {
                var memberUser = await _userManager.FindByIdAsync(userId);
                if (memberUser == null) continue;

                var member = new GroupMember
                {
                    GroupId = dto.GroupId,
                    UserId = userId,
                    UserName = memberUser.UserName ?? "",
                    IsAdmin = false,
                    JoinedAt = DateTime.UtcNow.AddHours(5.5)
                };

                _context.GroupMembers.Add(member);

                addedMembers.Add(new GroupMemberDto
                {
                    UserId = userId,
                    UserName = memberUser.UserName ?? "",
                    ProfileImage = memberUser.ProfileImage,
                    IsAdmin = false,
                    JoinedAt = member.JoinedAt
                });
            }

            await _context.SaveChangesAsync();
            return Ok(addedMembers);
        }

        [HttpPost("remove-member")]
        public async Task<IActionResult> RemoveMember([FromBody] RemoveMemberDto dto)
        {
            var currentUser = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentUser))
                return Unauthorized();

            var user = await _userManager.FindByNameAsync(currentUser);
            if (user == null) return NotFound();

            var isAdmin = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == dto.GroupId && gm.UserId == user.Id && gm.IsAdmin);

            if (!isAdmin && user.Id != dto.UserId)
                return Forbid("Only admins can remove members");

            var member = await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == dto.GroupId && gm.UserId == dto.UserId);

            if (member == null)
                return NotFound("Member not found");

            _context.GroupMembers.Remove(member);
            await _context.SaveChangesAsync();

            var remainingMembers = await _context.GroupMembers
                .Where(gm => gm.GroupId == dto.GroupId)
                .CountAsync();

            if (remainingMembers == 0)
            {
                var group = await _context.ChatGroups
                    .Include(g => g.Messages)
                    .FirstOrDefaultAsync(g => g.Id == dto.GroupId);

                if (group != null)
                {
                    _context.GroupMessages.RemoveRange(group.Messages);
            
                    _context.ChatGroups.Remove(group);
            
                    await _context.SaveChangesAsync();

                    return Ok(new { 
                        message = "Member removed successfully", 
                        groupDeleted = true,
                        groupId = dto.GroupId 
                    });
                }
            }

            return Ok(new { 
                message = "Member removed successfully", 
                groupDeleted = false 
            });
        }

        [HttpGet("{groupId}/details")]
        public async Task<IActionResult> GetGroupDetails(int groupId)
        {
            var currentUser = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentUser))
                return Unauthorized();

            var user = await _userManager.FindByNameAsync(currentUser);
            if (user == null) return NotFound();

            var isMember = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == user.Id);

            if (!isMember)
                return Forbid("You are not a member of this group");

            var group = await _context.ChatGroups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null)
                return NotFound("Group not found");

            var members = new List<GroupMemberDto>();
            foreach (var member in group.Members)
            {
                var memberUser = await _userManager.FindByIdAsync(member.UserId);
                members.Add(new GroupMemberDto
                {
                    UserId = member.UserId,
                    UserName = member.UserName,
                    ProfileImage = memberUser?.ProfileImage,
                    IsAdmin = member.IsAdmin,
                    JoinedAt = member.JoinedAt
                });
            }

            var details = new GroupDetailsDto
            {
                Id = group.Id,
                GroupName = group.GroupName,
                GroupImage = group.GroupImage,
                CreatedBy = group.CreatedBy,
                CreatedAt = group.CreatedAt,
                Members = members
            };

            return Ok(details);
        }

        [HttpPost("make-admin")]
        public async Task<IActionResult> MakeAdmin([FromBody] MakeMemberAdminDto dto)
        {
            var currentUser = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentUser))
                return Unauthorized();

            var user = await _userManager.FindByNameAsync(currentUser);
            if (user == null) return NotFound();

            var isAdmin = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == dto.GroupId && gm.UserId == user.Id && gm.IsAdmin);

            if (!isAdmin)
                return Forbid("Only admins can promote members");

            var member = await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == dto.GroupId && gm.UserId == dto.UserId);

            if (member == null)
                return NotFound("Member not found");

            member.IsAdmin = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Member promoted to admin" });
        }

        [HttpPost("remove-admin")]
        public async Task<IActionResult> RemoveAdmin([FromBody] MakeMemberAdminDto dto)
        {
            var currentUser = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentUser))
                return Unauthorized();

            var user = await _userManager.FindByNameAsync(currentUser);
            if (user == null) return NotFound();

            var isAdmin = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == dto.GroupId && gm.UserId == user.Id && gm.IsAdmin);

            if (!isAdmin)
                return Forbid("Only admins can demote members");

            var member = await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == dto.GroupId && gm.UserId == dto.UserId);

            if (member == null)
                return NotFound("Member not found");

            if (!member.IsAdmin)
                return BadRequest("Member is not an admin");

            var adminCount = await _context.GroupMembers
                .CountAsync(gm => gm.GroupId == dto.GroupId && gm.IsAdmin);

            if (adminCount <= 1)
                return BadRequest("Cannot remove the last admin. Please promote another member first.");

            member.IsAdmin = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Admin privileges removed" });
        }

        [HttpDelete("delete/{groupId}")]
        public async Task<IActionResult> DeleteGroup(int groupId)
        {
            var currentUser = User.Identity?.Name;
            if (string.IsNullOrEmpty(currentUser))
                return Unauthorized();

            var user = await _userManager.FindByNameAsync(currentUser);
            if (user == null) return NotFound();

            var isAdmin = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == user.Id && gm.IsAdmin);

            if (!isAdmin)
                return Forbid("Only admins can delete groups");

            var group = await _context.ChatGroups
                .Include(g => g.Members)
                .Include(g => g.Messages)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null)
                return NotFound("Group not found");

            var memberIds = group.Members.Select(m => m.UserId).ToList();

            _context.GroupMessages.RemoveRange(group.Messages);

            _context.GroupMembers.RemoveRange(group.Members);

            _context.ChatGroups.Remove(group);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Group deleted successfully", memberIds });
        }
    }
}
