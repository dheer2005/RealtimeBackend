namespace RealtimeChat.Dtos
{
    public class GroupDtos
    {
        public class MakeMemberAdminDto
        {
            public int GroupId { get; set; }
            public string UserId { get; set; } = string.Empty;
        }
        public class CreateGroupDto
        {
            public string GroupName { get; set; } = string.Empty;
            public string? GroupImage { get; set; }
            public List<string> MemberUserIds { get; set; } = new();
        }

        public class UpdateGroupDto
        {
            public int GroupId { get; set; }
            public string? GroupName { get; set; }
            public string? GroupImage { get; set; }
        }

        public class AddMembersDto
        {
            public int GroupId { get; set; }
            public List<string> UserIds { get; set; } = new();
        }

        public class RemoveMemberDto
        {
            public int GroupId { get; set; }
            public string UserId { get; set; } = string.Empty;
        }

        public class GroupDetailsDto
        {
            public int Id { get; set; }
            public string GroupName { get; set; } = string.Empty;
            public string? GroupImage { get; set; }
            public string CreatedBy { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public List<GroupMemberDto> Members { get; set; } = new();
            public GroupMessageDto? LastMessage { get; set; }
            public int UnreadCount { get; set; }
        }

        public class GroupMemberDto
        {
            public string UserId { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public string? ProfileImage { get; set; }
            public bool IsAdmin { get; set; }
            public DateTime JoinedAt { get; set; }
        }

        public class GroupMessageDto
        {
            public int Id { get; set; }
            public int GroupId { get; set; }
            public string FromUser { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public bool IsImage { get; set; }
            public string? MediaUrl { get; set; }
            public DateTime Created { get; set; }
            public string Status { get; set; } = string.Empty;
            public int? ReplyToMessageId { get; set; }
            public GroupMessageDto? ReplyTo { get; set; }
        }
    }
}
