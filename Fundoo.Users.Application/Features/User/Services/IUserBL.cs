
using Fundoo.Users.Domain.Entity;
using Fundoo.Users.Domain.Models;

namespace Fundoo.User.Application.Services
{
	public interface IUserBL
	{
        public Task<UserEntity> RegisterUserBL(UserEntity entity);
        public Task<UserEntity?> LoginUserBL(LoginModel loginUser);
        Task<bool> Enable2FAAsync(int userId, string secret);
        Task<UserEntity?> GetUserByIdAsync(int userId);
    }
}
