using Fundoo.Notes.Application;
using Fundoo.Notes.Domain;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace Fundoo.Notes.Infrastructure;

public class NotesRepository : INoteRepository
{
    private readonly string _connectionString;

    public NotesRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("NotesDb")!;
    }

    private NpgsqlConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    public async Task<List<Note>> GetNotesAsync(int userId, string category)
    {
        var notes = new List<Note>();
        var sql = "SELECT * FROM notes WHERE user_id = @userId";

        if (category == "active")
            sql += " AND is_archived = false AND is_trashed = false";

        if (category == "archived")
            sql += " AND is_archived = true AND is_trashed = false";

        if (category == "trash")
            sql += " AND is_trashed = true";

        sql += " ORDER BY created_at_utc DESC";

        await using var connection = CreateConnection();
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            notes.Add(ReadNote(reader));

        return notes;
    }

    public async Task<Note?> GetNoteAsync(int userId, Guid id)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT * FROM notes WHERE id=@id AND user_id=@userId", connection);

        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync();

        return await reader.ReadAsync() ? ReadNote(reader) : null;
    }

    public async Task AddNoteAsync(Note note)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        var sql = "INSERT INTO notes (id,user_id,title,content,color,is_pinned,is_archived,is_trashed,created_at_utc,updated_at_utc) VALUES (@id,@userId,@title,@content,@color,@pinned,false,false,@created,@updated)";

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("id", note.Id);
        command.Parameters.AddWithValue("userId", note.UserId);
        command.Parameters.AddWithValue("title", note.Title);
        command.Parameters.AddWithValue("content", note.Content);
        command.Parameters.AddWithValue("color", (object?)note.Color ?? DBNull.Value);
        command.Parameters.AddWithValue("pinned", note.IsPinned);
        command.Parameters.AddWithValue("created", note.CreatedAtUtc);
        command.Parameters.AddWithValue("updated", note.UpdatedAtUtc);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> UpdateNoteAsync(Note note)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        var sql = "UPDATE notes SET title=@title, content=@content, color=@color, is_pinned=@pinned, updated_at_utc=@updated WHERE id=@id AND user_id=@userId AND is_trashed=false";

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("title", note.Title);
        command.Parameters.AddWithValue("content", note.Content);
        command.Parameters.AddWithValue("color", (object?)note.Color ?? DBNull.Value);
        command.Parameters.AddWithValue("pinned", note.IsPinned);
        command.Parameters.AddWithValue("updated", DateTime.UtcNow);
        command.Parameters.AddWithValue("id", note.Id);
        command.Parameters.AddWithValue("userId", note.UserId);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteNoteAsync(int userId, Guid id)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "DELETE FROM notes WHERE id=@id AND user_id=@userId AND is_trashed=true",
            connection);

        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("userId", userId);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> SetNoteStateAsync(int userId, Guid id, string action)
    {
        string sql = action switch
        {
            "archive" => "UPDATE notes SET is_archived=true, updated_at_utc=@updated WHERE id=@id AND user_id=@userId AND is_trashed=false",
            "unarchive" => "UPDATE notes SET is_archived=false, updated_at_utc=@updated WHERE id=@id AND user_id=@userId AND is_trashed=false",
            "trash" => "UPDATE notes SET is_trashed=true, is_archived=false, updated_at_utc=@updated WHERE id=@id AND user_id=@userId",
            "restore" => "UPDATE notes SET is_trashed=false, updated_at_utc=@updated WHERE id=@id AND user_id=@userId AND is_trashed=true",
            _ => ""
        };

        if (sql == "")
            return false;

        await using var connection = CreateConnection();
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("updated", DateTime.UtcNow);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("userId", userId);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static Note ReadNote(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("id")),
        UserId = reader.GetInt32(reader.GetOrdinal("user_id")),
        Title = reader.GetString(reader.GetOrdinal("title")),
        Content = reader.GetString(reader.GetOrdinal("content")),
        Color = reader.IsDBNull(reader.GetOrdinal("color"))
            ? null
            : reader.GetString(reader.GetOrdinal("color")),
        IsPinned = reader.GetBoolean(reader.GetOrdinal("is_pinned")),
        IsArchived = reader.GetBoolean(reader.GetOrdinal("is_archived")),
        IsTrashed = reader.GetBoolean(reader.GetOrdinal("is_trashed")),
        CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("created_at_utc")),
        UpdatedAtUtc = reader.GetDateTime(reader.GetOrdinal("updated_at_utc"))
    };
}