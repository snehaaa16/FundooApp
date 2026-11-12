using Fundoo.Notes.Domain;
using MediatR;

namespace Fundoo.Notes.Application.Commands;

public record CreateNoteCommand(
    int UserId,
    string Title,
    string Content,
    string? Color,
    bool IsPinned
) : IRequest<Note>;

public class CreateNoteHandler : IRequestHandler<CreateNoteCommand, Note>
{
    private readonly INoteRepository _notes;
    private readonly INotesCacheService _cache;

    public CreateNoteHandler(INoteRepository notes, INotesCacheService cache)
    {
        _notes = notes;
        _cache = cache;
    }

    public async Task<Note> Handle(CreateNoteCommand request, CancellationToken cancellationToken)
    {
        var note = new Note
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Title = request.Title,
            Content = request.Content,
            Color = request.Color,
            IsPinned = request.IsPinned,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await _notes.AddNoteAsync(note);
        await _cache.InvalidateNotesCountAsync(request.UserId);

        return note;
    }
}
