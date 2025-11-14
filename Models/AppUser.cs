using Microsoft.AspNetCore.Identity;

namespace RealtimeChat.Models
{
    public class AppUser: IdentityUser
    {
        public string? FullName { get; set; }
        public string? ProfileImage {  get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
