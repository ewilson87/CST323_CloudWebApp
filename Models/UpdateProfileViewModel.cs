using System.ComponentModel.DataAnnotations;

namespace CloudWebApp.Models
{
    public class UpdateProfileViewModel
    {
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }
    }
}
