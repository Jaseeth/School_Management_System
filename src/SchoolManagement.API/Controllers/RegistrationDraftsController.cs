using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController, Route("api/registration-drafts"), Authorize(Roles = "Admin,Principal,Deputy Principal")]
public class RegistrationDraftsController(ApplicationDbContext db) : ControllerBase
{
    private string Owner => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private Task<bool> Active() => db.Users.AnyAsync(u => u.Id == Owner && u.IsActive);
    [HttpGet]
    public async Task<IActionResult> List(string kind)
    {
        if (!await Active()) return Unauthorized();
        if (kind is not ("staff" or "student")) return BadRequest();
        return Ok(await db.RegistrationDrafts.AsNoTracking().Where(d => d.OwnerUserId == Owner && d.Kind == kind)
            .OrderByDescending(d => d.UpdatedAt).Select(d => new { d.Id, d.Title, d.UpdatedAt }).ToListAsync());
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        if (!await Active()) return Unauthorized();
        var draft = await db.RegistrationDrafts.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && d.OwnerUserId == Owner);
        return draft == null ? NotFound() : Ok(new { draft.Id, draft.Kind, data = JsonSerializer.Deserialize<JsonElement>(draft.Data), draft.UpdatedAt });
    }
    public record SaveRequest(string Kind, JsonElement Data, DateTime? ExpectedUpdatedAt);
    [HttpPut("{id:guid}")]
    [RequestSizeLimit(65536)]
    public async Task<IActionResult> Save(Guid id, SaveRequest request)
    {
        if (!await Active()) return Unauthorized();
        if (id == Guid.Empty || request.Kind is not ("staff" or "student") || request.Data.ValueKind != JsonValueKind.Object || request.Data.GetRawText().Length > 40000) return BadRequest();
        var data = JsonNode.Parse(request.Data.GetRawText())!.AsObject();
        void Keep(JsonObject obj, string[] fields) { foreach (var key in obj.Select(p => p.Key).ToArray()) if (!fields.Contains(key)) obj.Remove(key); }
        if (request.Kind == "staff") Keep(data, ["staffNumber", "fullName", "email", "designation", "roleId", "canTeach", "teachingSubjectIds"]);
        else
        {
            Keep(data, ["studentForm", "parentForm", "academicYearId", "sectionId", "gradeId", "selectedSubjectIds", "currentStep"]);
            if (data["studentForm"] is JsonObject student) Keep(student, ["indexNumber", "fullName", "dateOfBirth", "email", "mobile", "schoolClassId"]);
            if (data["parentForm"] is JsonObject parent) Keep(parent, ["parentNumber", "fullName", "email", "phoneNumber", "relationship", "isPrimaryGuardian", "isEmergencyContact"]);
        }
        bool Scalar(JsonNode? node) => node == null || node is JsonValue;
        if (request.Kind == "staff") {
            if (data.Any(p => p.Key != "teachingSubjectIds" && p.Value != null && (p.Value is not JsonValue v || !v.TryGetValue<string>(out _)))) return BadRequest();
            if (data["canTeach"] is JsonValue teach && teach.TryGetValue<string>(out var answer) && answer is not ("" or "yes" or "no")) return BadRequest();
            if (data["teachingSubjectIds"] != null && (data["teachingSubjectIds"] is not JsonArray subjects || subjects.Count > 500 || subjects.Any(s => s is not JsonValue v || !v.TryGetValue<int>(out var subjectId) || subjectId <= 0))) return BadRequest();
        }
        if (request.Kind == "student")
        {
            if (data.Any(p => p.Key is not ("studentForm" or "parentForm" or "selectedSubjectIds") && !Scalar(p.Value))) return BadRequest();
            foreach (var key in new[] { "studentForm", "parentForm" })
                if (data[key] != null && (data[key] is not JsonObject obj || obj.Any(p => !Scalar(p.Value)))) return BadRequest();
            if (data["selectedSubjectIds"] != null && (data["selectedSubjectIds"] is not JsonArray subjects || subjects.Count > 500 || subjects.Any(s => s is not JsonValue v || !v.TryGetValue<int>(out _)))) return BadRequest();
        }
        var draft = await db.RegistrationDrafts.SingleOrDefaultAsync(d => d.Id == id);
        if (draft != null && draft.OwnerUserId != Owner) return NotFound();
        if (draft != null && (draft.Kind != request.Kind || draft.UpdatedAt != request.ExpectedUpdatedAt)) return Conflict(new { message = "This draft changed in another window. Reload it before saving." });
        if (draft == null && request.ExpectedUpdatedAt.HasValue) return Conflict(new { message = "This draft was deleted. Return to the list." });
        if (draft == null) { draft = new() { Id = id, OwnerUserId = Owner, Kind = request.Kind }; db.RegistrationDrafts.Add(draft); }
        var titleNode = request.Kind == "staff" ? data["fullName"] : data["studentForm"]?["fullName"];
        var title = titleNode is JsonValue titleValue && titleValue.TryGetValue<string>(out var name) ? name : "";
        draft.Title = string.IsNullOrWhiteSpace(title) ? "Untitled " + request.Kind + " draft" : title[..Math.Min(250, title.Length)];
        draft.Data = data.ToJsonString(); draft.UpdatedAt = DateTime.UtcNow;
        try { await db.SaveChangesAsync(); } catch (DbUpdateConcurrencyException) { return Conflict(new { message = "This draft changed in another window. Reload it before saving." }); }
        return Ok(new { draft.Id, draft.UpdatedAt });
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!await Active()) return Unauthorized();
        var count = await db.RegistrationDrafts.Where(d => d.Id == id && d.OwnerUserId == Owner).ExecuteDeleteAsync();
        return count == 0 ? NotFound() : NoContent();
    }
}
