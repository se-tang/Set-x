using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Master.Infrastructure;

namespace Master.Api.Controllers;

[ApiController]
[Route("api/traffic")]
[Authorize(Roles = "Admin")]
public class TrafficController : ControllerBase
{
    private readonly AppDbContext _db;

    public TrafficController(AppDbContext db) => _db = db;

    /// <summary>按服务器聚合（当天）</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] Guid? serverId, [FromQuery] Guid? userId,
        [FromQuery] string? date)
    {
        DateOnly d = date != null && DateOnly.TryParse(date, out var parsed)
            ? parsed : DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _db.TrafficRecords.Where(t => t.Date == d);
        if (serverId.HasValue) query = query.Where(t => t.ServerId == serverId.Value);
        if (userId.HasValue) query = query.Where(t => t.UserId == userId.Value);

        var rows = await query.ToListAsync();
        return Ok(new
        {
            date = d,
            total_upload = rows.Sum(r => r.UploadBytes),
            total_download = rows.Sum(r => r.DownloadBytes),
            total = rows.Sum(r => r.UploadBytes + r.DownloadBytes),
            by_server = rows.GroupBy(r => r.ServerId).Select(g => new
            {
                server_id = g.Key,
                upload = g.Sum(r => r.UploadBytes),
                download = g.Sum(r => r.DownloadBytes)
            }),
            by_user = rows.GroupBy(r => r.UserId).Select(g => new
            {
                user_id = g.Key,
                upload = g.Sum(r => r.UploadBytes),
                download = g.Sum(r => r.DownloadBytes)
            })
        });
    }

    /// <summary>30 天趋势</summary>
    [HttpGet("trend")]
    public async Task<IActionResult> Trend([FromQuery] Guid? serverId)
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-29));
        var query = _db.TrafficRecords.Where(t => t.Date >= start);
        if (serverId.HasValue) query = query.Where(t => t.ServerId == serverId.Value);

        var rows = await query.ToListAsync();
        var trend = rows.GroupBy(r => r.Date)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                date = g.Key,
                upload = g.Sum(r => r.UploadBytes),
                download = g.Sum(r => r.DownloadBytes)
            });
        return Ok(trend);
    }

    /// <summary>用户实时用量（含套餐限额对比）</summary>
    [HttpGet("users")]
    public async Task<IActionResult> UserUsage()
    {
        var users = await _db.Users.Where(u => u.Role == Master.Domain.Entities.UserRole.User).ToListAsync();
        var plans = await _db.UserPlans.Where(p => p.Active).ToListAsync();
        var traffic = await _db.TrafficRecords.ToListAsync();

        var result = users.Select(u =>
        {
            var up = plans.FirstOrDefault(p => p.UserId == u.Id);
            var used = traffic.Where(t => t.UserId == u.Id).Sum(t => t.UploadBytes + t.DownloadBytes);
            return new
            {
                u.Id, u.Username, u.Disabled,
                plan = up?.PlanId,
                plan_limit = up?.Plan.TrafficLimitBytes ?? 0,
                used_bytes = used,
                percent = up?.Plan.TrafficLimitBytes > 0
                    ? Math.Round(100.0 * used / up.Plan.TrafficLimitBytes, 1) : 0,
                over_limit = up?.Plan.TrafficLimitBytes > 0 && used >= up.Plan.TrafficLimitBytes,
                expire_at = up?.ExpireAt
            };
        });
        return Ok(result);
    }
}
