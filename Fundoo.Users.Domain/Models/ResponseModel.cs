using System;
namespace Fundoo.Users.Domain.Models
{
	public class ResponseModel<T>
	{
		public bool Success { get; set; } = true;
		public string Message { get; set; } = "";
		public T? Data { get; set; } = default(T);
	}
}

