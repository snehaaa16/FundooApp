using System;
namespace Fundoo.Users.Domain.Models
{
	public class ResetPasswordResponseModel
	{
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}

