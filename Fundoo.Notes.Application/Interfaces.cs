using Fundoo.Notes.Domain;

namespace Fundoo.Notes.Application;

public interface INoteRepository
{
    Task<List<Note>> GetNotesAsync(int userId, string category);
    Task<Note?> GetNoteAsync(int userId, Guid id);
    Task AddNoteAsync(Note note);
    Task<bool> UpdateNoteAsync(Note note);
    Task<bool> DeleteNoteAsync(int userId, Guid id);
    Task<bool> SetNoteStateAsync(int userId, Guid id, string action);
}

public interface INotesCacheService
{
    Task<Note?> GetNoteAsync(int userId, Guid noteId);
    Task SetNoteAsync(int userId, Note note);
    Task InvalidateNoteAsync(int userId, Guid noteId);
    Task<int?> GetNotesCountAsync(int userId);
    Task SetNotesCountAsync(int userId, int count);
    Task InvalidateNotesCountAsync(int userId);
}
