using System;
namespace Fundoo.Users.Domain.Models
{
	public class ResetPasswordRequestModel
	{
		public string Token { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}

