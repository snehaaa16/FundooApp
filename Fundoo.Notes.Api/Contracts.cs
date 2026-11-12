namespace Fundoo.Notes.Api;

public record CreateNoteRequest(string Title, string Content, string? Color, bool IsPinned);
public record UpdateNoteRequest(string Title, string Content, string? Color, bool IsPinned);
