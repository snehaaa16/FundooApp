using Fundoo.Notes.Domain;
using MediatR;

namespace Fundoo.Notes.Application.Queries;

public record GetNoteByIdResult(Note? Note, bool IsCacheHit);

public record GetNoteByIdQuery(int UserId, Guid NoteId) : IRequest<GetNoteByIdResult>;

public class GetNoteByIdHandler : IRequestHandler<GetNoteByIdQuery, GetNoteByIdResult>
{
    private readonly INoteRepository _notes;
    private readonly INotesCacheService _cache;

    public GetNoteByIdHandler(INoteRepository notes, INotesCacheService cache)
    {
        _notes = notes;
        _cache = cache;
    }

    public async Task<GetNoteByIdResult> Handle(GetNoteByIdQuery request, CancellationToken cancellationToken)
    {
        // 1. Try Redis cache
        var cached = await _cache.GetNoteAsync(request.UserId, request.NoteId);
        if (cached is not null)
        {
            return new GetNoteByIdResult(cached, true);
        }

        // 2. Cache miss -> query DB
        var note = await _notes.GetNoteAsync(request.UserId, request.NoteId);
        if (note is null)
        {
            return new GetNoteByIdResult(null, false);
        }

        // 3. Populate Redis cache
        await _cache.SetNoteAsync(request.UserId, note);
        return new GetNoteByIdResult(note, false);
    }
}
