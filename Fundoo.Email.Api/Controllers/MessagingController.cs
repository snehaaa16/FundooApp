using Microsoft.AspNetCore.Mvc;
using Fundoo.Notification.Domain.Models;
using Fundoo.Notification.Application.Feature.Notification.Services;

namespace Fundoo.Notification.Api.Controllers
{
    [ApiController]
    [Route("api/notification")]
	public class MessagingController:ControllerBase
	{
        private readonly IEmailService _email;

        public MessagingController(IEmailService _email)
        {
            this._email = _email;
        }

        [HttpPost]
        [Route("send")]
        public async Task<IActionResult> SendEmailAsync(EmailRequestModel req)
        {
            var res = await _email.SendEmailAsync(req);

            if (res.Success)
            {
                return Ok(res);
            }
            return BadRequest(res);
        }
    }
}

