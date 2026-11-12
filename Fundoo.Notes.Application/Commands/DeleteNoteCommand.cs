using MediatR;

namespace Fundoo.Notes.Application.Commands;

public record DeleteNoteCommand(
    int UserId,
    Guid NoteId
) : IRequest<bool>;

public class DeleteNoteHandler : IRequestHandler<DeleteNoteCommand, bool>
{
    private readonly INoteRepository _notes;
    private readonly INotesCacheService _cache;

    public DeleteNoteHandler(INoteRepository notes, INotesCacheService cache)
    {
        _notes = notes;
        _cache = cache;
    }

    public async Task<bool> Handle(DeleteNoteCommand request, CancellationToken cancellationToken)
    {
        var deleted = await _notes.DeleteNoteAsync(request.UserId, request.NoteId);
        if (deleted)
        {
            await _cache.InvalidateNoteAsync(request.UserId, request.NoteId);
            await _cache.InvalidateNotesCountAsync(request.UserId);
        }

        return deleted;
    }
}
