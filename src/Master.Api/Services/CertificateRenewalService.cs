using Master.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Master.Api.Services.Certificates;

namespace Master.Api.Services;

/// <summary>
/// 证书续期后台任务：每天检查 30 天内到期的证书并自动续期
/// </summary>
public class CertificateRenewalService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<CertificateRenewalService> _logger;

    public CertificateRenewalService(IServiceScopeFactory scopeFactory,
        IConfiguration config, ILogger<CertificateRenewalService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("证书续期服务已启动（每天检查）");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckRenewalsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "证书续期检查失败");
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task CheckRenewalsAsync(CancellationToken ct)
    {
        var cfToken = _config["Certificates:CloudflareApiToken"];
        if (string.IsNullOrEmpty(cfToken))
        {
            _logger.LogWarning("未配置 Cloudflare API Token，跳过续期检查");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var certService = scope.ServiceProvider.GetRequiredService<CertificateService>();

        var expiring = await db.Certificates
            .Where(c => c.ExpireAt <= DateTime.UtcNow.AddDays(30))
            .ToListAsync(ct);

        foreach (var cert in expiring)
        {
            try
            {
                var dns = new CloudflareDnsProvider(cfToken);
                var (certPem, keyPem) = await certService.IssueAsync(cert.Domain, dns,
                    _config["Certificates:AcmeEmail"] ?? "admin@setx.local", ct);
                cert.CertPem = certPem;
                cert.KeyPem = keyPem;
                cert.ExpireAt = DateTime.UtcNow.AddDays(90);
                cert.LastRenewedAt = DateTime.UtcNow;
                _logger.LogInformation("证书自动续期成功: {Domain}", cert.Domain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "证书自动续期失败: {Domain}", cert.Domain);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
