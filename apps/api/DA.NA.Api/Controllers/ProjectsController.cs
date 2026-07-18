using DA.NA.Api.Extensions;
using DA.NA.Core.Entities;
using DA.NA.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace DA.NA.Api.Controllers;

[ApiController]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProjectsController(AppDbContext db) => _db = db;

    /// <summary>
    /// List all projects for an organisation.
    /// Org users can only request projects for their own organisation.
    /// </summary>
    [HttpGet("api/organisations/{orgId:guid}/projects")]
    public async Task<IActionResult> GetByOrg(Guid orgId)
    {
        var callerOrgId = User.OrgId();
        if (callerOrgId.HasValue && callerOrgId.Value != orgId)
            return NotFound("Organisation not found");

        var orgExists = await _db.Organisations.AnyAsync(o => o.Id == orgId);
        if (!orgExists) return NotFound("Organisation not found");

        var projects = await _db.Projects
            .Where(p => p.OrgId == orgId)
            .Select(p => new
            {
                p.Id, p.Name, p.Region, p.Status,
                p.CreatedAt, p.LastSubmittedAt,
                AssessmentCount = p.Assessments.Count
            })
            .OrderBy(p => p.Name)
            .ToListAsync();

        return Ok(projects);
    }

    /// <summary>Get a single project.</summary>
    [HttpGet("api/projects/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var callerOrgId = User.OrgId();

        var project = await _db.Projects
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id, p.Name, p.Region, p.Status,
                p.OrgId, p.CreatedAt, p.LastSubmittedAt,
                Org = new { p.Org!.Id, p.Org.Name }
            })
            .FirstOrDefaultAsync();

        if (project is null) return NotFound();
        if (callerOrgId.HasValue && project.OrgId != callerOrgId.Value)
            return NotFound();

        return Ok(project);
    }

    /// <summary>
    /// Create a project under an organisation.
    /// Org users can only create projects within their own organisation.
    /// </summary>
    [HttpPost("api/organisations/{orgId:guid}/projects")]
    public async Task<IActionResult> Create(Guid orgId, [FromBody] CreateProjectRequest req)
    {
        var callerOrgId = User.OrgId();
        if (callerOrgId.HasValue && callerOrgId.Value != orgId)
            return NotFound("Organisation not found");

        var orgExists = await _db.Organisations.AnyAsync(o => o.Id == orgId);
        if (!orgExists) return NotFound("Organisation not found");

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = req.Name,
            Region = req.Region,
            Status = ProjectStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = project.Id },
            new { project.Id, project.Name, project.Region, project.Status, project.OrgId, project.CreatedAt });
    }

    /// <summary>Update a project's name, region, or status.</summary>
    [HttpPatch("api/projects/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectRequest req)
    {
        var callerOrgId = User.OrgId();

        var project = await _db.Projects.FindAsync(id);
        if (project is null) return NotFound();
        if (callerOrgId.HasValue && project.OrgId != callerOrgId.Value)
            return NotFound();

        if (req.Name is not null)    project.Name = req.Name;
        if (req.Region is not null)  project.Region = req.Region;
        if (req.Status.HasValue)     project.Status = req.Status.Value;

        await _db.SaveChangesAsync();
        return Ok(new { project.Id, project.Name, project.Region, project.Status, project.OrgId, project.CreatedAt });
    }

    /// <summary>Delete a project. Hard delete for prototype.</summary>
    [HttpDelete("api/projects/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var callerOrgId = User.OrgId();

        var project = await _db.Projects.FindAsync(id);
        if (project is null) return NotFound();
        if (callerOrgId.HasValue && project.OrgId != callerOrgId.Value)
            return NotFound();

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

/// <param name="Name">Display name of the project.</param>
/// <param name="Region">Optional geographic region (e.g. "Greece", "Lebanon").</param>
public record CreateProjectRequest(
    [Required(AllowEmptyStrings = false)] string Name,
    string? Region);

/// <param name="Name">New display name. Omit or pass null to leave unchanged.</param>
/// <param name="Region">New region. Omit or pass null to leave unchanged.</param>
/// <param name="Status">Set to Active or Inactive. Omit or pass null to leave unchanged.</param>
public record UpdateProjectRequest(string? Name, string? Region, ProjectStatus? Status);
