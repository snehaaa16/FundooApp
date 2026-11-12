using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using Fundoo.Notes.Application;
using Fundoo.Notes.Domain;

namespace Fundoo.Notes.Infrastructure;

/// <summary>
/// Encapsulates all Redis caching logic for Notes.
/// Keys:
///   note:{userId}:{noteId}       → cached Note object (JSON)   TTL: 10 min
///   notes:count:{userId}         → total notes count (string)  TTL: 5 min
/// </summary>
public class NotesCacheService : INotesCacheService
{
    private readonly IDistributedCache _cache;

    public NotesCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    // ── Single note ──────────────────────────────────────────────────────────

    public async Task<Note?> GetNoteAsync(int userId, Guid noteId)
    {
        var key = NoteKey(userId, noteId);
        var json = await _cache.GetStringAsync(key);
        return json is null ? null : JsonSerializer.Deserialize<Note>(json);
    }

    public async Task SetNoteAsync(int userId, Note note)
    {
        var key = NoteKey(userId, note.Id);
        var json = JsonSerializer.Serialize(note);
        await _cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        });
    }

    public async Task InvalidateNoteAsync(int userId, Guid noteId)
        => await _cache.RemoveAsync(NoteKey(userId, noteId));

    // ── Notes count ──────────────────────────────────────────────────────────

    public async Task<int?> GetNotesCountAsync(int userId)
    {
        var val = await _cache.GetStringAsync(CountKey(userId));
        return val is null ? null : int.Parse(val);
    }

    public async Task SetNotesCountAsync(int userId, int count)
        => await _cache.SetStringAsync(CountKey(userId), count.ToString(),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

    public async Task InvalidateNotesCountAsync(int userId)
        => await _cache.RemoveAsync(CountKey(userId));

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static string NoteKey(int userId, Guid noteId)  => $"note:{userId}:{noteId}";
    private static string CountKey(int userId)               => $"notes:count:{userId}";
}
