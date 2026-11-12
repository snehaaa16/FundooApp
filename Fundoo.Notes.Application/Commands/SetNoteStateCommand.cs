using MediatR;

namespace Fundoo.Notes.Application.Commands;

public record SetNoteStateCommand(
    int UserId,
    Guid NoteId,
    string Action
) : IRequest<bool>;

public class SetNoteStateHandler : IRequestHandler<SetNoteStateCommand, bool>
{
    private readonly INoteRepository _notes;
    private readonly INotesCacheService _cache;

    public SetNoteStateHandler(INoteRepository notes, INotesCacheService cache)
    {
        _notes = notes;
        _cache = cache;
    }

    public async Task<bool> Handle(SetNoteStateCommand request, CancellationToken cancellationToken)
    {
        await _cache.InvalidateNoteAsync(request.UserId, request.NoteId);

        // If trash or restore, count of active notes changes
        if (request.Action == "trash" || request.Action == "restore")
        {
            await _cache.InvalidateNotesCountAsync(request.UserId);
        }

        return await _notes.SetNoteStateAsync(request.UserId, request.NoteId, request.Action);
    }
}
