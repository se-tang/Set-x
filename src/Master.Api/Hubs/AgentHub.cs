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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<NotificationHub> _notifyHub;
    private static readonly Dictionary<Guid, string> Connections = new();

    public AgentHub(AppDbContext db, IServiceScopeFactory scopeFactory, IHubContext<NotificationHub> notifyHub)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _notifyHub = notifyHub;
    }

    public static string? GetConnectionId(Guid serverId) =>
        Connections.TryGetValue(serverId, out var cid) ? cid : null;

    public override async Task OnConnectedAsync()
    {
        var serverId = ValidateRequest();
        if (serverId == null)
        {
            Context.Abort();
            return;
        }

        Connections[serverId.Value] = Context.ConnectionId;
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
        var entry = Connections.FirstOrDefault(kv => kv.Value == Context.ConnectionId);
        if (entry.Key != Guid.Empty)
        {
            Connections.Remove(entry.Key);
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
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var server = await db.Servers.FindAsync(dto.ServerId);
                if (server == null) return;

                server.LastSeenAt = DateTime.UtcNow;
                server.Status = ServerStatus.Online;

                // 流量落库（增量累加到当天记录）
                if (dto.UploadBytes > 0 || dto.DownloadBytes > 0)
                {
                    var today = DateOnly.FromDateTime(DateTime.UtcNow);
                    var record = await db.TrafficRecords
                        .FirstOrDefaultAsync(t => t.ServerId == dto.ServerId && t.Date == today);
                    if (record == null)
                    {
                        record = new TrafficRecord
                        {
                            Id = Guid.NewGuid(),
                            ServerId = dto.ServerId,
                            NodeId = Guid.Empty,
                            UserId = Guid.Empty, // 用户维度待节点绑定后细分
                            Date = today,
                            UploadBytes = dto.UploadBytes,
                            DownloadBytes = dto.DownloadBytes
                        };
                        db.TrafficRecords.Add(record);
                    }
                    else
                    {
                        record.UploadBytes += dto.UploadBytes;
                        record.DownloadBytes += dto.DownloadBytes;
                    }
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"状态上报落库失败: {ex.Message}");
            }
        });
        return Task.CompletedTask;
    }

    public Task ReportConfigApplyResult(Guid nodeId, bool success, string? error)
    {
        // 独立 scope：不依赖 Hub 生命周期（避免 disposed context race）
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var node = await db.Nodes.FindAsync(nodeId);
                if (node != null)
                {
                    node.DeployStatus = success ? NodeDeployStatus.Success : NodeDeployStatus.Failed;
                    node.DeployError = success ? null : error;
                    node.DeployedAt = DateTime.UtcNow;
                    if (!success) node.Enabled = false;
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"节点回执落库失败: {ex.Message}");
            }
            // 广播给前端（部署状态变更）
            await _notifyHub.Clients.All.SendAsync("NodeDeployStatusChanged", nodeId, success, error);
        });
        return Task.CompletedTask;
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

    public record AgentStatusDto(Guid ServerId, bool XrayAlive, int CpuPercent, int MemMb,
        long UploadBytes, long DownloadBytes, long Timestamp);
}
