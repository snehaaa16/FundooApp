using Fundoo.User.Application.Services;
using Fundoo.User.Domain.Models;
using Fundoo.Users.Domain.Entity;
using Fundoo.Users.Domain.Models;


namespace Fundoo.User.Infrastructure.Services.Users
{
	public class UserBL: IUserBL
    {
        private IUserRL _userRL;

        public UserBL(IUserRL _userRL)
        {
            this._userRL = _userRL;
        }

        public async Task<UserEntity> RegisterUserBL(UserEntity entity)
        {
            return await _userRL.RegisterUserRL(entity);
        }

        public async Task<UserEntity?> LoginUserBL(LoginModel loginUser)
        {
            return await _userRL.LoginUser(loginUser);
        }

        public async Task<bool> Enable2FAAsync(int userId, string secret)
            => await _userRL.Enable2FAAsync(userId, secret);

        public async Task<UserEntity?> GetUserByIdAsync(int userId)
            => await _userRL.GetUserByIdAsync(userId);
    }
}
