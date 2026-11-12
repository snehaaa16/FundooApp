using Fundoo.Notes.Domain;
using MediatR;

namespace Fundoo.Notes.Application.Commands;

public record UpdateNoteCommand(
    int UserId,
    Guid NoteId,
    string Title,
    string Content,
    string? Color,
    bool IsPinned
) : IRequest<bool>;

public class UpdateNoteHandler : IRequestHandler<UpdateNoteCommand, bool>
{
    private readonly INoteRepository _notes;
    private readonly INotesCacheService _cache;

    public UpdateNoteHandler(INoteRepository notes, INotesCacheService cache)
    {
        _notes = notes;
        _cache = cache;
    }

    public async Task<bool> Handle(UpdateNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _notes.GetNoteAsync(request.UserId, request.NoteId);
        if (note == null)
            return false;

        note.Title = request.Title;
        note.Content = request.Content;
        note.Color = request.Color;
        note.IsPinned = request.IsPinned;

        var updated = await _notes.UpdateNoteAsync(note);
        if (updated)
        {
            await _cache.InvalidateNoteAsync(request.UserId, request.NoteId);
        }

        return updated;
    }
}
