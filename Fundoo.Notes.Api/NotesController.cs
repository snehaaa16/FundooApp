using System.Security.Claims;
using Fundoo.Notes.Application.Commands;
using Fundoo.Notes.Application.Queries;
using Fundoo.Notes.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.Notes.Api;

[ApiController]
[Authorize]
[Route("api/notes")]
public class NotesController : ControllerBase
{
    private readonly ISender _sender;

    public NotesController(ISender sender)
    {
        _sender = sender;
    }

    private int CurrentUserId()
    {
        var idClaim = User.FindFirst("user_id")
                   ?? User.FindFirst(ClaimTypes.NameIdentifier)
                   ?? User.FindFirst("sub");

        if (idClaim != null && int.TryParse(idClaim.Value, out var id))
            return id;

        throw new UnauthorizedAccessException("User ID claim is missing or invalid in the token.");
    }

    // ── GET /api/notes (Active notes) ─────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetNotes()
    {
        int uid = CurrentUserId();
        var notes = await _sender.Send(new GetNotesQuery(uid, "active"));

        Response.Headers["X-Total-Notes"] = notes.Count.ToString();
        return Ok(notes);
    }

    // ── GET /api/notes/archived ──────────────────────────────────────────────
    [HttpGet("archived")]
    public async Task<IActionResult> GetArchived()
    {
        int uid = CurrentUserId();
        var notes = await _sender.Send(new GetNotesQuery(uid, "archived"));
        return Ok(notes);
    }

    // ── GET /api/notes/trash ─────────────────────────────────────────────────
    [HttpGet("trash")]
    public async Task<IActionResult> GetTrash()
    {
        int uid = CurrentUserId();
        var notes = await _sender.Send(new GetNotesQuery(uid, "trash"));
        return Ok(notes);
    }

    // ── GET /api/notes/count (Redis-backed Notes Count) ───────────────────────
    [HttpGet("count")]
    public async Task<IActionResult> GetNotesCount()
    {
        int uid = CurrentUserId();
        var result = await _sender.Send(new GetNotesCountQuery(uid));
        return Ok(new { count = result.Count, source = result.Source });
    }

    // ── GET /api/notes/{id} (Cache-Aside Note) ────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetNote(Guid id)
    {
        int uid = CurrentUserId();
        var result = await _sender.Send(new GetNoteByIdQuery(uid, id));

        if (result.Note is null)
            return NotFound();

        Response.Headers["X-Cache"] = result.IsCacheHit ? "HIT" : "MISS";
        return Ok(result.Note);
    }

    // ── POST /api/notes (Create Note) ─────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create(CreateNoteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "Title and content are required." });

        int uid = CurrentUserId();
        var command = new CreateNoteCommand(uid, request.Title, request.Content, request.Color, request.IsPinned);
        var note = await _sender.Send(command);

        return CreatedAtAction(nameof(GetNote), new { id = note.Id }, note);
    }

    // ── PUT /api/notes/{id} (Update Note) ─────────────────────────────────────
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateNoteRequest request)
    {
        int uid = CurrentUserId();
        var command = new UpdateNoteCommand(uid, id, request.Title, request.Content, request.Color, request.IsPinned);
        var updated = await _sender.Send(command);

        return updated ? NoContent() : NotFound();
    }

    // ── State Changes ────────────────────────────────────────────────────────
    [HttpPut("{id:guid}/archive")]
    public async Task<bool> Archive(Guid id)
        => await _sender.Send(new SetNoteStateCommand(CurrentUserId(), id, "archive"));

    [HttpPut("{id:guid}/unarchive")]
    public async Task<bool> Unarchive(Guid id)
        => await _sender.Send(new SetNoteStateCommand(CurrentUserId(), id, "unarchive"));

    [HttpPut("{id:guid}/trash")]
    public async Task<bool> Trash(Guid id)
        => await _sender.Send(new SetNoteStateCommand(CurrentUserId(), id, "trash"));

    [HttpPut("{id:guid}/restore")]
    public async Task<bool> Restore(Guid id)
        => await _sender.Send(new SetNoteStateCommand(CurrentUserId(), id, "restore"));

    // ── DELETE /api/notes/{id} ────────────────────────────────────────────────
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _sender.Send(new DeleteNoteCommand(CurrentUserId(), id));
        return deleted ? NoContent() : NotFound();
    }
}
