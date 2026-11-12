using System;
namespace Fundoo.Users.Domain.Models
{
	public class NoteCreationModel
	{
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Colour { get; set; } = string.Empty;
    }
}

