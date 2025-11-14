using System.ComponentModel.DataAnnotations;

namespace RealtimeChat.Dtos
{
    public class EditProfilePicDto
    {
        [Required]
        public IFormFile NewProfileImage { get; set; }
    }
}
