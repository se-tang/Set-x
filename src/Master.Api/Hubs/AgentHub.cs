using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Master.Domain.Entities;
using Master.Infrastructure;

namespace Master.Api.Hubs;

public class AgentHub : Hub
{
    private readonly AppDbContext _db;
    private static readonly Dictionary<Guid, string> _connections = new();

    public AgentHub(AppDbContext db) => _db = db;

    public static string? GetConnectionId(Guid serverId) =>
        _connections.TryGetValue(serverId, out var cid) ? cid : null;

    public override async Task OnConnectedAsync()
    {
        var serverId = ValidateRequest();
        if (serverId == null)
        {
            Context.Abort();
            return;
        }

        _connections[serverId.Value] = Context.ConnectionId;
        var server = await _db.Servers.FindAsync(serverId.Value);
        if (server != null)
        {
            server.Status = ServerStatus.Online;
            server.LastSeenAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var entry = _connections.FirstOrDefault(kv => kv.Value == Context.ConnectionId);
        if (entry.Key != Guid.Empty)
        {
            _connections.Remove(entry.Key);
            var server = await _db.Servers.FindAsync(entry.Key);
            if (server != null)
            {
                server.Status = ServerStatus.Offline;
                await _db.SaveChangesAsync();
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    // Agent → Master
    public Task ReportStatus(AgentStatusDto dto)
    {
        // 状态上报：记录 LastSeen（流量聚合在 Step 9 实现）
        _ = Task.Run(async () =>
        {
            var server = await _db.Servers.FindAsync(dto.ServerId);
            if (server != null)
            {
                server.LastSeenAt = DateTime.UtcNow;
                server.Status = ServerStatus.Online;
                await _db.SaveChangesAsync();
            }
        });
        return Task.CompletedTask;
    }

    public async Task ReportConfigApplyResult(Guid nodeId, bool success, string? error)
    {
        if (nodeId != Guid.Empty)
        {
            var node = await _db.Nodes.FindAsync(nodeId);
            if (node != null && !success)
            {
                node.Enabled = false;
                await _db.SaveChangesAsync();
            }
        }
    }

    private Guid? ValidateRequest()
    {
        var httpCtx = Context.GetHttpContext();
        if (httpCtx == null) return null;

        var serverIdStr = httpCtx.Request.Headers["X-Server-Id"].ToString();
        var timestamp = httpCtx.Request.Headers["X-Timestamp"].ToString();
        var signature = httpCtx.Request.Headers["X-Signature"].ToString();

        if (!Guid.TryParse(serverIdStr, out var serverId)) return null;

        // 时间戳防重放（±5 分钟）
        if (!long.TryParse(timestamp, out var ts)) return null;
        if (Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > 300) return null;

        var server = _db.Servers.Find(serverId);
        if (server == null) return null;

        // HMAC 签名校验
        var payload = $"{serverIdStr}|{ts}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(server.AgentTokenHash));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        if (!string.Equals(expected, signature, StringComparison.OrdinalIgnoreCase)) return null;

        return serverId;
    }

    public record AgentStatusDto(Guid ServerId, bool XrayAlive, int CpuPercent, int MemMb, long Timestamp);
}
