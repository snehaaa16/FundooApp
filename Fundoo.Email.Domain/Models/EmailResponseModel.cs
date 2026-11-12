using System;
namespace Fundoo.Notification.Domain.Models
{
	public class EmailResponseModel
	{
        public bool Success { get; set; }
        public string Message { get; set; } = "" ;
    }
}

