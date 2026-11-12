using Fundoo.User.Domain.Models;
using Fundoo.Users.Domain.Models;
using MediatR;
using System;

namespace Fundoo.User.Application.Command
{
	public class LoginCommand: IRequest<UserResponseModel?>
    {
        public LoginModel _loginModel;

		public LoginCommand(LoginModel loginModel)
		{
			_loginModel = loginModel;
		}
	}
}

