using Fundoo.User.Application.Services;
using Fundoo.Users.Domain.Entity;
using Fundoo.Users.Domain.Models;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.Users.Infrastructure.Persistence.Repositories
{
    public class UserRL : IUserRL
    {
        FundooContext fundooContext;

        public UserRL(FundooContext fundooContext)
        {
            this.fundooContext = fundooContext;
        }

        public async Task<UserEntity> RegisterUserRL(UserEntity userEntity)
        {
            await fundooContext.Users.AddAsync(userEntity);

            await fundooContext.SaveChangesAsync();

            return userEntity;
        }

        public async Task<UserEntity> LoginUser(LoginModel loginuser)
        {
            string email = loginuser.Email;

            UserEntity? user = await fundooContext.Users.FirstOrDefaultAsync(e => e.Email == email);

            return user!;
        }


        public async Task<ForgotPasswordResponseModel> ForgotPasswordAsyncRL(ForgotPasswordRequestModel req)
        {
            var cleanEmail = req.Email.Trim().ToLower();
            bool containsEmail = await fundooContext.Users.AnyAsync(e => e.Email.ToLower() == cleanEmail);

            if (containsEmail) return new ForgotPasswordResponseModel()
            {
                Message = "Email exists",
                Success = true
            };

            return new ForgotPasswordResponseModel()
            {
                Message = "Email doesn't exists",
                Success = false
            };
        }

        public async Task<ResetPasswordResponseModel> ResetPasswordAsyncRL(ResetPasswordRequestModel req)
        {
            var cleanEmail = req.Email.Trim().ToLower();
            var user = await fundooContext.Users.FirstOrDefaultAsync(e => e.Email.ToLower() == cleanEmail);

            if (user == null)
            {
                return new ResetPasswordResponseModel()
                {
                    Success = false,
                    Message = "user not found"
                };
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);

            await fundooContext.SaveChangesAsync();

            return new ResetPasswordResponseModel()
            {
                Success = true,
                Message = "Password Changed Successfully"
            };

        }

        public async Task<bool> Enable2FAAsync(int userId, string secret)
        {
            var user = await fundooContext.Users.FindAsync(userId);
            if (user == null) return false;

            user.TwoFactorSecret    = secret;
            user.IsTwoFactorEnabled = !string.IsNullOrEmpty(secret);
            await fundooContext.SaveChangesAsync();
            return true;
        }

        public async Task<UserEntity?> GetUserByIdAsync(int userId)
            => await fundooContext.Users.FindAsync(userId);
    }
}
