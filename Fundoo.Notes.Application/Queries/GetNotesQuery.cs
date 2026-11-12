using Fundoo.Notes.Domain;
using MediatR;

namespace Fundoo.Notes.Application.Queries;

public record GetNotesQuery(int UserId, string Category) : IRequest<List<Note>>;

public class GetNotesHandler : IRequestHandler<GetNotesQuery, List<Note>>
{
    private readonly INoteRepository _notes;
    private readonly INotesCacheService _cache;

    public GetNotesHandler(INoteRepository notes, INotesCacheService cache)
    {
        _notes = notes;
        _cache = cache;
    }

    public async Task<List<Note>> Handle(GetNotesQuery request, CancellationToken cancellationToken)
    {
        var notes = await _notes.GetNotesAsync(request.UserId, request.Category);

        if (request.Category == "active")
        {
            // Cache active count
            await _cache.SetNotesCountAsync(request.UserId, notes.Count);
        }

        return notes;
    }
}
