using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Master.Domain.Entities;
using Master.Infrastructure;

namespace Master.Api.Controllers;

[ApiController]
[Route("api/nodes")]
[Authorize(Roles = "Admin")]
public class NodesController : ControllerBase
{
    private readonly AppDbContext _db;

    public NodesController(AppDbContext db) => _db = db;

    /// <summary>全局节点列表（支持筛选：服务器/协议/部署状态）</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? serverId, [FromQuery] string? protocol,
        [FromQuery] int? deployStatus, [FromQuery] bool? enabled)
    {
        var query = _db.Nodes.Include(n => n.Server).Include(n => n.Bindings).AsQueryable();
        if (serverId.HasValue) query = query.Where(n => n.ServerId == serverId.Value);
        if (!string.IsNullOrEmpty(protocol))
            query = query.Where(n => n.Protocol == Enum.Parse<ProxyProtocol>(protocol, true));
        if (deployStatus.HasValue)
            query = query.Where(n => (int)n.DeployStatus == deployStatus.Value);
        if (enabled.HasValue) query = query.Where(n => n.Enabled == enabled.Value);

        var nodes = await query.OrderByDescending(n => n.CreatedAt).ToListAsync();
        return Ok(nodes.Select(n => new
        {
            n.Id, n.Name, n.Protocol, n.Port, n.Enabled, n.ConfigJson,
            n.RateMultiplier, n.DeployStatus, n.DeployError, n.DeployedAt, n.CreatedAt,
            ServerId = n.ServerId,
            ServerName = n.Server?.Name,
            BindUserCount = n.Bindings?.Count ?? 0
        }));
    }

    /// <summary>批量启用/禁用</summary>
    [HttpPost("batch-enabled")]
    public async Task<IActionResult> BatchEnabled([FromBody] BatchEnabledRequest req)
    {
        var nodes = await _db.Nodes.Where(n => req.NodeIds.Contains(n.Id)).ToListAsync();
        foreach (var n in nodes) n.Enabled = req.Enabled;
        await _db.SaveChangesAsync();
        return Ok(new { success = true, count = nodes.Count });
    }

    /// <summary>批量删除</summary>
    [HttpPost("batch-delete")]
    public async Task<IActionResult> BatchDelete([FromBody] BatchRequest req)
    {
        var nodes = await _db.Nodes.Where(n => req.NodeIds.Contains(n.Id)).ToListAsync();
        _db.Nodes.RemoveRange(nodes);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, count = nodes.Count });
    }

    public record BatchRequest(Guid[] NodeIds);
    public record BatchEnabledRequest(Guid[] NodeIds, bool Enabled);
}
