namespace RealtimeChat.Models
{
    public class ChatGroup
    {
        public int Id { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string? GroupImage { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
        public ICollection<GroupMessage> Messages { get; set; } = new List<GroupMessage>();
    }
}
