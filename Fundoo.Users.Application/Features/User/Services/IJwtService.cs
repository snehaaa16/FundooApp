using Fundoo.User.Domain.Models;
using Fundoo.Users.Domain.Models;

namespace Fundoo.User.Application.Services
{
	public interface IJwtService
	{
        string GenerateToken(UserResponseModel user);

        string GeneratePasswordResetToken(string email);

        string? ValidatePasswordResetToken(string token);

    }
}