using MediatR;

namespace Fundoo.Notes.Application.Queries;

public record GetNotesCountResult(int Count, string Source);

public record GetNotesCountQuery(int UserId) : IRequest<GetNotesCountResult>;

public class GetNotesCountHandler : IRequestHandler<GetNotesCountQuery, GetNotesCountResult>
{
    private readonly INoteRepository _notes;
    private readonly INotesCacheService _cache;

    public GetNotesCountHandler(INoteRepository notes, INotesCacheService cache)
    {
        _notes = notes;
        _cache = cache;
    }

    public async Task<GetNotesCountResult> Handle(GetNotesCountQuery request, CancellationToken cancellationToken)
    {
        // 1. Try Redis cache
        var cached = await _cache.GetNotesCountAsync(request.UserId);
        if (cached.HasValue)
        {
            return new GetNotesCountResult(cached.Value, "cache");
        }

        // 2. Cache miss -> query DB
        var notes = await _notes.GetNotesAsync(request.UserId, "active");
        await _cache.SetNotesCountAsync(request.UserId, notes.Count);

        return new GetNotesCountResult(notes.Count, "db");
    }
}
