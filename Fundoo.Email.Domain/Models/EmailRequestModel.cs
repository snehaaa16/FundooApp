using System;
namespace Fundoo.Notification.Domain.Models
{
	public class EmailRequestModel
	{
		public string To { get; set; } = "";
		public string Subject { get; set; } = "";
		public string Body { get; set; } = "";
        public bool IsHtml { get; set; }
    }
}

