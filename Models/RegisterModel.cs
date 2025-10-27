using System.ComponentModel.DataAnnotations;

namespace RealtimeChat.Models
{
    public class RegisterModel
    {
        [Required(ErrorMessage ="Username is required")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Full name is required")]
        public string FullName { get; set; }

        [Required(ErrorMessage ="Phone number is required")]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "Phone number must be of 10 digits")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email formats")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be atleast 6 characters")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Profile image is required")]
        public IFormFile? ProfileImage { get; set; }
    }
}
