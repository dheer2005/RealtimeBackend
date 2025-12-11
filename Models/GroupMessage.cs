namespace RealtimeChat.Models
{
    public class GroupMessage
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public string FromUser { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsImage { get; set; }
        public string? MediaUrl { get; set; }
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "sent";
        public int? ReplyToMessageId { get; set; }

        public ChatGroup Group { get; set; } = null!;
    }
}
