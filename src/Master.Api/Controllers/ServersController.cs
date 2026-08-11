using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Master.Domain.Entities;
using Master.Infrastructure;
using Master.Api.Hubs;
using Master.Api.Services;

namespace Master.Api.Controllers;

[ApiController]
[Route("api/servers")]
[Authorize(Roles = "Admin")]
public class ServersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<AgentHub> _hub;

    public ServersController(AppDbContext db, IHubContext<AgentHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await _db.Servers.OrderBy(s => s.CreatedAt).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServerRequest req)
    {
        // 生成一次性安装令牌（HMAC 密钥，30 分钟有效）
        var token = GenerateToken(32);
        var server = new Server
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Region = req.Region ?? string.Empty,
            IpAddress = req.IpAddress ?? string.Empty,
            AgentTokenHash = HashToken(token),
            Status = ServerStatus.Installing
        };
        _db.Servers.Add(server);
        await _db.SaveChangesAsync();

        // 返回安装命令（含明文 token——只此一次）
        var installCmd = $"curl -sL http://YOUR-MASTER/agent-install.sh | sudo bash -s -- --token={token} --master=http://YOUR-MASTER --server-id={server.Id}";
        return Ok(new { server, install_command = installCmd, install_token = token });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var server = await _db.Servers.FindAsync(id);
        if (server == null) return NotFound();
        var nodes = await _db.Nodes.Where(n => n.ServerId == id).ToListAsync();
        _db.Nodes.RemoveRange(nodes);
        _db.Servers.Remove(server);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id)
    {
        var server = await _db.Servers.FindAsync(id);
        if (server == null) return NotFound();
        var nodes = await _db.Nodes.Where(n => n.ServerId == id).ToListAsync();
        return Ok(new { server, nodes });
    }

    [HttpPost("{id:guid}/nodes")]
    public async Task<IActionResult> CreateNode(Guid id, [FromBody] Node node)
    {
        var server = await _db.Servers.FindAsync(id);
        if (server == null) return NotFound();
        node.Id = Guid.NewGuid();
        node.ServerId = id;
        _db.Nodes.Add(node);
        await _db.SaveChangesAsync();
        await PushConfigAsync(id);
        return Ok(node);
    }

    [HttpPatch("nodes/{id:guid}")]
    public async Task<IActionResult> UpdateNode(Guid id, [FromBody] Node req)
    {
        var node = await _db.Nodes.FindAsync(id);
        if (node == null) return NotFound();
        node.Name = req.Name;
        node.Protocol = req.Protocol;
        node.Port = req.Port;
        node.ConfigJson = req.ConfigJson;
        node.Enabled = req.Enabled;
        node.RateMultiplier = req.RateMultiplier;
        await _db.SaveChangesAsync();
        await PushConfigAsync(node.ServerId);
        return Ok(node);
    }

    [HttpDelete("nodes/{id:guid}")]
    public async Task<IActionResult> DeleteNode(Guid id)
    {
        var node = await _db.Nodes.FindAsync(id);
        if (node == null) return NotFound();
        var serverId = node.ServerId;
        _db.Nodes.Remove(node);
        await _db.SaveChangesAsync();
        await PushConfigAsync(serverId);
        return Ok(new { success = true });
    }

    /// <summary>生成该服务器完整 xray 配置并通过 SignalR 推送给在线 Agent</summary>
    private async Task PushConfigAsync(Guid serverId)
    {
        var server = await _db.Servers.FindAsync(serverId);
        if (server == null) return;
        var nodes = await _db.Nodes.Where(n => n.ServerId == serverId).ToListAsync();
        var config = XrayConfigBuilder.Build(server, nodes);
        var nodeIds = nodes.Where(n => n.Enabled).Select(n => n.Id).ToArray();

        // 标记为下发中
        foreach (var n in nodes.Where(n => n.Enabled))
        {
            n.DeployStatus = NodeDeployStatus.Applying;
            n.DeployError = null;
        }
        await _db.SaveChangesAsync();

        var connectionId = AgentHub.GetConnectionId(serverId);
        if (connectionId != null)
            await _hub.Clients.Client(connectionId).SendAsync("UpdateXrayConfig", config, nodeIds);
        else
        {
            // Agent 离线——标记失败
            foreach (var n in nodes.Where(n => n.Enabled))
            {
                n.DeployStatus = NodeDeployStatus.Failed;
                n.DeployError = "Agent 离线，无法下发";
            }
            await _db.SaveChangesAsync();
        }
    }

    [HttpPost("nodes/{id:guid}/bindings")]
    public async Task<IActionResult> BindUser(Guid id, [FromBody] BindUserRequest req)
    {
        var binding = new NodeUserBinding
        {
            Id = Guid.NewGuid(),
            NodeId = id,
            UserId = req.UserId,
            PerUserConfigJson = req.PerUserConfigJson ?? "{}"
        };
        _db.NodeUserBindings.Add(binding);
        await _db.SaveChangesAsync();
        return Ok(binding);
    }

    private static string GenerateToken(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[RandomNumberGenerator.GetInt32(s.Length)]).ToArray());
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    public record CreateServerRequest(string Name, string? Region, string? IpAddress);
    public record BindUserRequest(Guid UserId, string? PerUserConfigJson);
}
