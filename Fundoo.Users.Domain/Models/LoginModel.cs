using System;
using System.ComponentModel.DataAnnotations;

namespace Fundoo.Users.Domain.Models
{
	public class LoginModel
	{
		[Required(ErrorMessage ="Email is required")]
		[EmailAddress(ErrorMessage ="Enter a valid email")]
		public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage ="Password is required")]
		public string Password { get; set; } = string.Empty;
    }
}

