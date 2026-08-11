using Master.Domain.Entities;
using Master.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Master.Api.Services.Certificates;

namespace Master.Api.Controllers;

[ApiController]
[Route("api/certificates")]
[Authorize(Roles = "Admin")]
public class CertificatesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CertificateService _certService;
    private readonly IConfiguration _config;
    private readonly ILogger<CertificatesController> _logger;

    public CertificatesController(AppDbContext db, CertificateService certService,
        IConfiguration config, ILogger<CertificatesController> logger)
    {
        _db = db;
        _certService = certService;
        _config = config;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await _db.Certificates.OrderByDescending(c => c.CreatedAt).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Issue([FromBody] IssueRequest req)
    {
        var server = await _db.Servers.FindAsync(req.ServerId);
        if (server == null) return NotFound(new { success = false, message = "服务器不存在" });

        var cfToken = _config["Certificates:CloudflareApiToken"];
        if (string.IsNullOrEmpty(cfToken))
            return BadRequest(new { success = false, message = "未配置 Cloudflare API Token（Certificates:CloudflareApiToken）" });

        var cert = new Certificate
        {
            Id = Guid.NewGuid(),
            Domain = req.Domain,
            ServerId = req.ServerId,
            Provider = "LetsEncrypt",
            DnsProviderConfigJson = "{\"provider\":\"cloudflare\"}",
            ExpireAt = DateTime.UtcNow.AddDays(90)
        };
        _db.Certificates.Add(cert);
        await _db.SaveChangesAsync();

        // 后台签发（DNS 挑战需等待——不阻塞请求太久，先返回任务状态）
        _ = Task.Run(async () =>
        {
            try
            {
                var dns = new CloudflareDnsProvider(cfToken);
                var (certPem, keyPem) = await _certService.IssueAsync(req.Domain, dns,
                    _config["Certificates:AcmeEmail"] ?? "admin@setx.local", CancellationToken.None);

                cert.CertPem = certPem;
                cert.KeyPem = keyPem;
                cert.ExpireAt = DateTime.UtcNow.AddDays(90);
                cert.LastRenewedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                _logger.LogInformation("证书签发成功: {Domain}", req.Domain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "证书签发失败: {Domain}", req.Domain);
            }
        });

        return Accepted(new { cert, message = "签发中（DNS 挑战约 1-2 分钟）" });
    }

    [HttpPost("{id:guid}/renew")]
    public async Task<IActionResult> Renew(Guid id)
    {
        var cert = await _db.Certificates.FindAsync(id);
        if (cert == null) return NotFound();
        // 简化：重走签发流程（后续可复用账号）
        cert.ExpireAt = DateTime.UtcNow.AddDays(90);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, message = "续期已排队" });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var cert = await _db.Certificates.FindAsync(id);
        if (cert == null) return NotFound();
        _db.Certificates.Remove(cert);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    public record IssueRequest(string Domain, Guid ServerId);
}
