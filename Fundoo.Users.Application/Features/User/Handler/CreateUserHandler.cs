using Fundoo.User.Application.Command;
using Fundoo.User.Application.Services;
using Fundoo.User.Domain.Models;
using Fundoo.Users.Domain.Entity;
using Fundoo.Users.Domain.Models;
using MediatR;

namespace Fundoo.User.Application.Handler
{
	public class CreateUserHandler: IRequestHandler<CreateUserCommand, UserResponseModel>
    {
        private IUserBL _userBL;

        public CreateUserHandler(IUserBL userBL )
		{
            _userBL = userBL;
		}

        public async Task<UserResponseModel> Handle(CreateUserCommand command, CancellationToken cancellationToken)
        {
            UserEntity savedUser = await _userBL.RegisterUserBL(command.User);

            return new UserResponseModel
            {
                UserId = savedUser.UserId,
                Fname = savedUser.FirstName,
                Lname = savedUser.LastName,
                Email = savedUser.Email,
                ContactNo = savedUser.PhoneNumber
            };
        }
    }
}

