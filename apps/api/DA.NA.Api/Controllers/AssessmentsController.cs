using DA.NA.Api.Extensions;
using DA.NA.Core.Entities;
using DA.NA.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace DA.NA.Api.Controllers;

[ApiController]
[Authorize]
public class AssessmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AssessmentsController(AppDbContext db) => _db = db;

    // Returns the orgId for a given project, or null if the project doesn't exist.
    private async Task<Guid?> ProjectOrgIdAsync(Guid projectId) =>
        await _db.Projects.Where(p => p.Id == projectId).Select(p => (Guid?)p.OrgId).FirstOrDefaultAsync();

    // ── Assessment endpoints ──────────────────────────────────────────────────

    /// <summary>List all assessments for a project, newest first.</summary>
    [HttpGet("api/projects/{projectId:guid}/assessments")]
    public async Task<IActionResult> GetByProject(Guid projectId)
    {
        var projectOrgId = await ProjectOrgIdAsync(projectId);
        if (projectOrgId is null) return NotFound("Project not found");

        var callerOrgId = User.OrgId();
        if (callerOrgId.HasValue && callerOrgId.Value != projectOrgId.Value)
            return NotFound("Project not found");

        var assessments = await _db.NeedsAssessments
            .Where(a => a.ProjectId == projectId)
            .OrderByDescending(a => a.SubmittedAt ?? a.CreatedAt)
            .Select(a => new
            {
                a.Id, Status = a.Status.ToString(), a.Notes,
                a.CreatedAt, a.SubmittedAt, a.SupersedesId,
                ItemCount = a.Items.Count
            })
            .ToListAsync();

        return Ok(assessments);
    }

    /// <summary>
    /// Get the latest submitted assessment for a project (summary shape — id, status,
    /// notes, timestamps, item count). Projected to avoid returning the tracked entity
    /// graph, which would form an Items[].Assessment back-reference cycle that
    /// System.Text.Json cannot serialize.
    /// </summary>
    [HttpGet("api/projects/{projectId:guid}/assessments/current")]
    public async Task<IActionResult> GetCurrent(Guid projectId)
    {
        var projectOrgId = await ProjectOrgIdAsync(projectId);
        if (projectOrgId is null) return NotFound("Project not found");

        var callerOrgId = User.OrgId();
        if (callerOrgId.HasValue && callerOrgId.Value != projectOrgId.Value)
            return NotFound("Project not found");

        var assessment = await _db.NeedsAssessments
            .Where(a => a.ProjectId == projectId && a.Status == AssessmentStatus.Submitted)
            .OrderByDescending(a => a.SubmittedAt)
            .Select(a => new
            {
                a.Id, a.Status, a.Notes,
                a.CreatedAt, a.SubmittedAt,
                ItemCount = a.Items.Count
            })
            .FirstOrDefaultAsync();

        if (assessment is null)
            return NotFound("No submitted assessment found for this project");

        return Ok(assessment);
    }

    // Shared "assessment with items" response shape — projected (not the tracked entity) to
    // avoid the Items[].Assessment serialization cycle. Used by GetById and WorkingDraft.
    private static object ToDto(NeedsAssessment a) => new
    {
        a.Id,
        Status = a.Status.ToString(),
        a.SupersedesId,
        a.CreatedAt,
        a.SubmittedAt,
        Items = a.Items.Select(i => new
        {
            i.Id, i.ItemTypeId, i.Quantity, i.UnitId,
            ItemType = new { i.ItemType.Name, i.ItemType.Category },
            Unit = new { i.Unit.Name }
        })
    };

    /// <summary>Get a single assessment by ID, including all items.</summary>
    [HttpGet("api/assessments/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var assessment = await _db.NeedsAssessments
            .Include(a => a.Items).ThenInclude(i => i.ItemType)
            .Include(a => a.Items).ThenInclude(i => i.Unit)
            .Include(a => a.Project)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assessment is null) return NotFound();

        var callerOrgId = User.OrgId();
        if (callerOrgId.HasValue && assessment.Project.OrgId != callerOrgId.Value)
            return NotFound();

        return Ok(ToDto(assessment));
    }

    /// <summary>
    /// Return the project's single editable working draft: the existing open draft if one
    /// exists, otherwise a new draft seeded with a copy of the latest submitted assessment's items.
    /// </summary>
    [HttpPost("api/projects/{projectId:guid}/assessments/working-draft")]
    public async Task<IActionResult> WorkingDraft(Guid projectId)
    {
        var projectOrgId = await ProjectOrgIdAsync(projectId);
        if (projectOrgId is null) return NotFound("Project not found");

        var callerOrgId = User.OrgId();
        if (callerOrgId.HasValue && callerOrgId.Value != projectOrgId.Value)
            return NotFound("Project not found");

        var existing = await _db.NeedsAssessments
            .Include(a => a.Items).ThenInclude(i => i.ItemType)
            .Include(a => a.Items).ThenInclude(i => i.Unit)
            .Where(a => a.ProjectId == projectId && a.Status == AssessmentStatus.Draft)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();
        if (existing is not null) return Ok(ToDto(existing));

        var current = await _db.NeedsAssessments
            .Include(a => a.Items)
            .Where(a => a.ProjectId == projectId && a.Status == AssessmentStatus.Submitted)
            .OrderByDescending(a => a.SubmittedAt)
            .FirstOrDefaultAsync();

        var callerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var draft = new NeedsAssessment
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CreatedBy = callerId,
            Status = AssessmentStatus.Draft,
            SupersedesId = current?.Id,
            CreatedAt = DateTime.UtcNow,
            Items = current is null ? new List<AssessmentItem>() : current.Items.Select(i => new AssessmentItem
            {
                Id = Guid.NewGuid(),
                ItemTypeId = i.ItemTypeId,
                Quantity = i.Quantity,
                UnitId = i.UnitId,
                Notes = i.Notes,
                CreatedAt = DateTime.UtcNow
            }).ToList()
        };
        _db.NeedsAssessments.Add(draft);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Lost a race to create the single open draft — return the winner's draft.
            _db.Entry(draft).State = EntityState.Detached;
            var winner = await _db.NeedsAssessments
                .Include(a => a.Items).ThenInclude(i => i.ItemType)
                .Include(a => a.Items).ThenInclude(i => i.Unit)
                .Where(a => a.ProjectId == projectId && a.Status == AssessmentStatus.Draft)
                .OrderByDescending(a => a.CreatedAt)
                .FirstAsync();
            return Ok(ToDto(winner));
        }

        await _db.Entry(draft).Collection(a => a.Items).Query()
            .Include(i => i.ItemType).Include(i => i.Unit).LoadAsync();
        return CreatedAtAction(nameof(GetById), new { id = draft.Id }, ToDto(draft));
    }

    /// <summary>
    /// Create a new draft assessment for a project.
    /// Pass SupersedesId if this is an update to an existing submitted assessment.
    /// </summary>
    [HttpPost("api/projects/{projectId:guid}/assessments")]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateAssessmentRequest req)
    {
        var projectOrgId = await ProjectOrgIdAsync(projectId);
        if (projectOrgId is null) return NotFound("Project not found");

        var callerOrgId = User.OrgId();
        if (callerOrgId.HasValue && callerOrgId.Value != projectOrgId.Value)
            return NotFound("Project not found");

        var callerId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var assessment = new NeedsAssessment
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CreatedBy = callerId,
            Status = AssessmentStatus.Draft,
            SupersedesId = req.SupersedesId,
            Notes = req.Notes,
            CreatedAt = DateTime.UtcNow
        };
        _db.NeedsAssessments.Add(assessment);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = assessment.Id }, assessment);
    }

    /// <summary>
    /// Submit a draft assessment. Validates at least one item exists.
    /// Once submitted, the assessment is immutable — create a new one to make changes.
    /// </summary>
    [HttpPost("api/assessments/{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id)
    {
        var assessment = await _db.NeedsAssessments
            .Include(a => a.Items)
            .Include(a => a.Project)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assessment is null) return NotFound();

        var callerOrgId = User.OrgId();
        if (callerOrgId.HasValue && assessment.Project.OrgId != callerOrgId.Value)
            return NotFound();

        if (assessment.Status == AssessmentStatus.Submitted)
            return BadRequest("Assessment is already submitted.");
        if (!assessment.Items.Any())
            return BadRequest("Cannot submit an assessment with no items. Add at least one item first.");

        assessment.Status = AssessmentStatus.Submitted;
        assessment.SubmittedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(assessment);
    }

    /// <summary>Update the notes on a draft assessment.</summary>
    [HttpPatch("api/assessments/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAssessmentRequest req)
    {
        var assessment = await _db.NeedsAssessments
            .Include(a => a.Project)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assessment is null) return NotFound();

        var callerOrgId = User.OrgId();
        if (callerOrgId.HasValue && assessment.Project.OrgId != callerOrgId.Value)
            return NotFound();

        if (assessment.Status == AssessmentStatus.Submitted)
            return BadRequest("Cannot edit a submitted assessment. Create a new one instead.");

        if (req.Notes is not null) assessment.Notes = req.Notes;
        await _db.SaveChangesAsync();
        return Ok(assessment);
    }

    /// <summary>Delete a draft assessment. Submitted assessments cannot be deleted.</summary>
    [HttpDelete("api/assessments/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var assessment = await _db.NeedsAssessments
            .Include(a => a.Project)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assessment is null) return NotFound();

        var callerOrgId = User.OrgId();
        if (callerOrgId.HasValue && assessment.Project.OrgId != callerOrgId.Value)
            return NotFound();

        if (assessment.Status == AssessmentStatus.Submitted)
            return BadRequest("Cannot delete a submitted assessment.");

        _db.NeedsAssessments.Remove(assessment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── Item endpoints ────────────────────────────────────────────────────────

    /// <summary>Add an item to a draft assessment.</summary>
    [HttpPost("api/assessments/{id:guid}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddItemRequest req)
    {
        var assessment = await _db.NeedsAssessments
            .Include(a => a.Project)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assessment is null) return NotFound("Assessment not found");

        var callerOrgId = User.OrgId();
        if (callerOrgId.HasValue && assessment.Project.OrgId != callerOrgId.Value)
            return NotFound("Assessment not found");

        if (assessment.Status == AssessmentStatus.Submitted)
            return BadRequest("Cannot add items to a submitted assessment.");

        var itemTypeExists = await _db.ItemTypes.AnyAsync(i => i.Id == req.ItemTypeId);
        if (!itemTypeExists) return BadRequest("Item type not found. Call GET /api/categories to get valid item type IDs.");

        var unitExists = await _db.Units.AnyAsync(u => u.Id == req.UnitId);
        if (!unitExists) return BadRequest("Unit not found. Call GET /api/units to get valid unit IDs.");

        var item = new AssessmentItem
        {
            Id = Guid.NewGuid(),
            AssessmentId = id,
            ItemTypeId = req.ItemTypeId,
            Quantity = req.Quantity,
            UnitId = req.UnitId,
            Notes = req.Notes,
            CreatedAt = DateTime.UtcNow
        };
        _db.AssessmentItems.Add(item);
        await _db.SaveChangesAsync();

        // Return with navigation properties loaded
        await _db.Entry(item).Reference(i => i.ItemType).LoadAsync();
        await _db.Entry(item).Reference(i => i.Unit).LoadAsync();
        return CreatedAtAction(nameof(GetById), new { id }, item);
    }

    /// <summary>Update an item's quantity, unit, or notes on a draft assessment.</summary>
    [HttpPatch("api/assessments/{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> UpdateItem(Guid id, Guid itemId, [FromBody] UpdateItemRequest req)
    {
        var assessment = await _db.NeedsAssessments
            .Include(a => a.Project)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assessment is null) return NotFound("Assessment not found");

        var callerOrgId = User.OrgId();
        if (callerOrgId.HasValue && assessment.Project.OrgId != callerOrgId.Value)
            return NotFound("Assessment not found");

        if (assessment.Status == AssessmentStatus.Submitted)
            return BadRequest("Cannot edit items on a submitted assessment.");

        var item = await _db.AssessmentItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.AssessmentId == id);
        if (item is null) return NotFound("Item not found on this assessment");

        if (req.Quantity.HasValue) item.Quantity = req.Quantity.Value;
        if (req.UnitId.HasValue)   item.UnitId = req.UnitId.Value;
        if (req.Notes is not null) item.Notes = req.Notes;

        await _db.SaveChangesAsync();
        return Ok(item);
    }

    /// <summary>Remove an item from a draft assessment.</summary>
    [HttpDelete("api/assessments/{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id, Guid itemId)
    {
        var assessment = await _db.NeedsAssessments
            .Include(a => a.Project)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assessment is null) return NotFound("Assessment not found");

        var callerOrgId = User.OrgId();
        if (callerOrgId.HasValue && assessment.Project.OrgId != callerOrgId.Value)
            return NotFound("Assessment not found");

        if (assessment.Status == AssessmentStatus.Submitted)
            return BadRequest("Cannot delete items from a submitted assessment.");

        var item = await _db.AssessmentItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.AssessmentId == id);
        if (item is null) return NotFound("Item not found on this assessment");

        _db.AssessmentItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

/// <param name="Notes">Optional free-text notes for the assessment.</param>
/// <param name="SupersedesId">ID of the previous assessment this one replaces. Null for a brand new assessment.</param>
public record CreateAssessmentRequest(string? Notes, Guid? SupersedesId);

/// <param name="Notes">Updated notes. Omit or pass null to leave unchanged.</param>
public record UpdateAssessmentRequest(string? Notes);

/// <param name="ItemTypeId">ID of the item type. Get valid IDs from GET /api/categories.</param>
/// <param name="Quantity">How many units are needed. Must be greater than zero.</param>
/// <param name="UnitId">ID of the unit. Get valid IDs from GET /api/units.</param>
/// <param name="Notes">Optional free-text notes for this item (e.g. size preferences).</param>
public record AddItemRequest(
    Guid ItemTypeId,
    [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    decimal Quantity,
    Guid UnitId,
    string? Notes);

/// <param name="Quantity">New quantity. Omit or pass null to leave unchanged.</param>
/// <param name="UnitId">New unit. Omit or pass null to leave unchanged.</param>
/// <param name="Notes">Updated notes. Omit or pass null to leave unchanged.</param>
public record UpdateItemRequest(decimal? Quantity, Guid? UnitId, string? Notes);
