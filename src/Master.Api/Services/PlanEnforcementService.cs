using Master.Domain.Entities;
using Master.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Master.Api.Services;

/// <summary>
/// 后台任务：每小时扫描
/// 1. 套餐到期 → 禁用用户
/// 2. 流量超限 → 禁用用户（Active=false）
/// </summary>
public class PlanEnforcementService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlanEnforcementService> _logger;

    public PlanEnforcementService(IServiceScopeFactory scopeFactory, ILogger<PlanEnforcementService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("套餐执行服务已启动（每小时扫描）");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "套餐扫描失败");
            }
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        // 1. 套餐到期
        var expired = await db.UserPlans
            .Where(p => p.Active && p.ExpireAt <= now)
            .Include(p => p.User)
            .ToListAsync(ct);

        foreach (var up in expired)
        {
            up.Active = false;
            if (up.User != null)
            {
                up.User.Disabled = true;
                _logger.LogWarning("套餐到期禁用用户 {User}（{Plan}）", up.User.Username, up.PlanId);
            }
        }

        // 2. 流量超限
        var activePlans = await db.UserPlans
            .Where(p => p.Active && p.Plan.TrafficLimitBytes > 0)
            .Include(p => p.Plan)
            .Include(p => p.User)
            .ToListAsync(ct);

        foreach (var up in activePlans)
        {
            var used = await db.TrafficRecords
                .Where(t => t.UserId == up.UserId)
                .SumAsync(t => (long)t.UploadBytes + t.DownloadBytes, ct);
            if (used >= up.Plan.TrafficLimitBytes && up.User != null)
            {
                up.Active = false;
                up.User.Disabled = true;
                _logger.LogWarning("流量超限禁用用户 {User}（used={Used}/{Limit}）",
                    up.User.Username, used, up.Plan.TrafficLimitBytes);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
