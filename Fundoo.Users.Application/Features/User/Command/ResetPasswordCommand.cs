using Fundoo.User.Domain.Models;
using Fundoo.Users.Domain.Models;
using MediatR;
using System;

namespace Fundoo.User.Application.Command
{
    public class ResetPasswordCommand : IRequest<ResetPasswordResponseModel>
    {
        public ResetPasswordRequestModel Request { get; }

        public ResetPasswordCommand(ResetPasswordRequestModel request)
        {
            Request = request;
        }
    }
}

