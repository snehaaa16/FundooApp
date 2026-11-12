using Fundoo.Users.Domain.Entity;
using Fundoo.Users.Domain.Models;
using ModelLayer.Models;

namespace Fundoo.User.Application.Services
{
	public interface IUserRL
	{
        public Task<UserEntity> RegisterUserRL(UserEntity userEntity);
        public Task<UserEntity> LoginUser(LoginModel loginuser);
        public Task<ForgotPasswordResponseModel> ForgotPasswordAsyncRL(ForgotPasswordRequestModel req);
        public Task<ResetPasswordResponseModel> ResetPasswordAsyncRL(ResetPasswordRequestModel req);
        Task<bool> Enable2FAAsync(int userId, string secret);
        Task<UserEntity?> GetUserByIdAsync(int userId);
    }
}
