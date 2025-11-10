namespace RealtimeChat.Models
{
    public class UserSession
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string JwtId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }

        public string? DeviceInfo { get; set; }
        public string? IpAddress { get; set; }
    }
}
