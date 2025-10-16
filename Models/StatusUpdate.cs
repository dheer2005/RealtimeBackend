namespace RealtimeChat.Models
{
    public class StatusUpdate
    {
        public int Id { get; set; } // Message ID
        public string Status { get; set; } = "sent";
    }
}
