using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Master.Domain.Entities;
using Master.Infrastructure;

namespace Master.Api.Controllers;

[ApiController]
[Route("api/plans")]
[Authorize(Roles = "Admin")]
public class PlansController : ControllerBase
{
    private readonly AppDbContext _db;

    public PlansController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await _db.Plans.OrderBy(p => p.Price).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Plan plan)
    {
        plan.Id = Guid.NewGuid();
        _db.Plans.Add(plan);
        await _db.SaveChangesAsync();
        return Ok(plan);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Plan req)
    {
        var plan = await _db.Plans.FindAsync(id);
        if (plan == null) return NotFound();
        plan.Name = req.Name;
        plan.TrafficLimitBytes = req.TrafficLimitBytes;
        plan.SpeedLimitMbps = req.SpeedLimitMbps;
        plan.DurationDays = req.DurationDays;
        plan.Price = req.Price;
        await _db.SaveChangesAsync();
        return Ok(plan);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var plan = await _db.Plans.FindAsync(id);
        if (plan == null) return NotFound();
        _db.Plans.Remove(plan);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }
}

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await _db.Users.Select(u => new
        {
            u.Id, u.Username, u.Email, u.Role, u.Disabled, u.SubscriptionToken, u.CreatedAt
        }).ToListAsync());

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest req)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        user.Disabled = req.Disabled ?? user.Disabled;
        if (!string.IsNullOrWhiteSpace(req.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpPost("{id:guid}/plans")]
    public async Task<IActionResult> BindPlan(Guid id, [FromBody] BindPlanRequest req)
    {
        var user = await _db.Users.FindAsync(id);
        var plan = await _db.Plans.FindAsync(req.PlanId);
        if (user == null || plan == null) return NotFound();

        var up = new UserPlan
        {
            Id = Guid.NewGuid(),
            UserId = id,
            PlanId = req.PlanId,
            StartAt = DateTime.UtcNow,
            ExpireAt = DateTime.UtcNow.AddDays(plan.DurationDays),
            UsedTrafficBytes = 0,
            Active = true
        };
        _db.UserPlans.Add(up);
        await _db.SaveChangesAsync();
        return Ok(up);
    }

    [HttpGet("{id:guid}/plans")]
    public async Task<IActionResult> GetPlans(Guid id) =>
        Ok(await _db.UserPlans.Where(p => p.UserId == id).ToListAsync());

    public record UpdateUserRequest(bool? Disabled, string? Password);
    public record BindPlanRequest(Guid PlanId);
}
