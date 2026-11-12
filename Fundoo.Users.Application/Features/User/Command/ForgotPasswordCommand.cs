using Fundoo.User.Domain.Models;
using Fundoo.Users.Domain.Models;
using MediatR;
using ModelLayer.Models;
using System;

namespace Fundoo.User.Application.Command
{
	public class ForgotPasswordCommand : IRequest<ForgotPasswordResponseModel>
    {
        public ForgotPasswordRequestModel Request { get; }

        public ForgotPasswordCommand(ForgotPasswordRequestModel request)
        {
            Request = request;
        }
    }
}

