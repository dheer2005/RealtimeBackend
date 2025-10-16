namespace RealtimeChat.Models
{
    public class FriendRequest
    {
        public int Id { get; set; }
        public string FromUserId { get; set; } = string.Empty;
        public string ToUserId { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; //Pending, Accepted, Rejected
        public DateTime RequestedAt { get; set; } = DateTime.Now;
        public DateTime RespondedAt {  get; set; }

        public AppUser FromUser { get; set; }
        public AppUser ToUser { get; set; }
    }
}
