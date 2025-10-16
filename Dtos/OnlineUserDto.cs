namespace RealtimeChat.Dtos
{
    public class OnlineUserDto
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public string ProfileImage { get; set; } = string.Empty;
    }
}
