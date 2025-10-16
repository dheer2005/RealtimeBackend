namespace RealtimeChat.Models
{
    public class Group_chats
    {
        public int Id { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string FromUser { get; set; }
        public string Message { get; set; }
        public DateTime Created {  get; set; }
        public string Status { get; set; } = "sent";
    }
}
