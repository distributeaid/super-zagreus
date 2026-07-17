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
[Route("api/organisations")]
public class OrganisationsController : ControllerBase
{
    private readonly AppDbContext _db;
    public OrganisationsController(AppDbContext db) => _db = db;

    /// <summary>
    /// List organisations. DA users see all. Org users see only their own organisation.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var callerOrgId = User.OrgId();

        var query = _db.Organisations.AsQueryable();
        if (callerOrgId.HasValue)
            query = query.Where(o => o.Id == callerOrgId.Value);

        var orgs = await query
            .Select(o => new
            {
                o.Id, o.Name, o.SalesforceId,
                o.CreatedAt, o.LastSubmittedAt,
                ProjectCount = o.Projects.Count
            })
            .OrderBy(o => o.Name)
            .ToListAsync();

        return Ok(orgs);
    }

    /// <summary>Get a single organisation including its projects.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var callerOrgId = User.OrgId();
        if (callerOrgId.HasValue && callerOrgId.Value != id)
            return NotFound();

        var org = await _db.Organisations
            .Where(o => o.Id == id)
            .Select(o => new
            {
                o.Id, o.Name, o.SalesforceId,
                o.CreatedAt, o.LastSubmittedAt,
                Projects = o.Projects.Select(p => new
                {
                    p.Id, p.Name, p.Region, p.Status, p.CreatedAt, p.LastSubmittedAt
                })
            })
            .FirstOrDefaultAsync();

        if (org is null) return NotFound();
        return Ok(org);
    }

    /// <summary>Create a new organisation.</summary>
    [Authorize(Policy = "DaAdmin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrganisationRequest req)
    {
        var org = new Organisation
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            SalesforceId = req.SalesforceId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Organisations.Add(org);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = org.Id }, new { org.Id, org.Name, org.SalesforceId, org.CreatedAt });
    }

    /// <summary>Update an organisation's name or Salesforce ID.</summary>
    [Authorize(Policy = "DaAdmin")]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrganisationRequest req)
    {
        var org = await _db.Organisations.FindAsync(id);
        if (org is null) return NotFound();

        if (req.Name is not null)         org.Name = req.Name;
        if (req.SalesforceId is not null) org.SalesforceId = req.SalesforceId;

        await _db.SaveChangesAsync();
        return Ok(new { org.Id, org.Name, org.SalesforceId, org.CreatedAt });
    }

    /// <summary>Delete an organisation. Hard delete for prototype — use with caution.</summary>
    [Authorize(Policy = "DaAdmin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var org = await _db.Organisations.FindAsync(id);
        if (org is null) return NotFound();
        _db.Organisations.Remove(org);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

/// <param name="Name">Display name of the organisation.</param>
/// <param name="SalesforceId">Optional Salesforce CRM ID for cross-system reference.</param>
public record CreateOrganisationRequest(
    [Required(AllowEmptyStrings = false)] string Name,
    string? SalesforceId);

/// <param name="Name">New display name. Omit or pass null to leave unchanged.</param>
/// <param name="SalesforceId">New Salesforce ID. Omit or pass null to leave unchanged.</param>
public record UpdateOrganisationRequest(string? Name, string? SalesforceId);
