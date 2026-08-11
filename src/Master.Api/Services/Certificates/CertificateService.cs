using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using Microsoft.Extensions.Logging;

namespace Master.Api.Services.Certificates;

/// <summary>
/// ACME 证书签发服务（Let's Encrypt DNS-01）
/// Certes 2.5 API：无 CancellationToken 参数（同步 Task API）
/// </summary>
public class CertificateService
{
    private readonly ILogger<CertificateService> _logger;

    public CertificateService(ILogger<CertificateService> logger) => _logger = logger;

    /// <summary>
    /// 签发证书（DNS-01 挑战）
    /// </summary>
    /// <returns>(证书PEM, 私钥PEM)</returns>
    public async Task<(string CertPem, string KeyPem)> IssueAsync(
        string domain, IDnsProvider dns, string acmeEmail, CancellationToken ct)
    {
        var acme = new AcmeContext(WellKnownServers.LetsEncryptV2);
        var account = await acme.NewAccount(acmeEmail, termsOfServiceAgreed: true);
        var order = await acme.NewOrder(new[] { domain });
        var authz = (await order.Authorizations()).First();

        var dnsChallenge = await authz.Dns();
        var txtValue = acme.AccountKey.DnsTxt(dnsChallenge.Token);

        _logger.LogInformation("创建 DNS TXT 记录: _acme-challenge.{Domain}", domain);
        await dns.CreateTxtRecordAsync(domain, txtValue, ct);

        try
        {
            // 轮询验证（不固定 sleep——等待 DNS 生效）
            var validated = false;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                var challenge = await dnsChallenge.Validate();
                if (challenge.Status == ChallengeStatus.Valid)
                {
                    validated = true;
                    break;
                }
                _logger.LogInformation("DNS 挑战验证中...（{Attempt}/30）", attempt + 1);
            }

            if (!validated)
                throw new InvalidOperationException("DNS 挑战验证超时（30 次 × 5s）");

            // 等待订单 ready 并签发
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var refreshed = await order.Resource();
                if (refreshed.Status == OrderStatus.Ready) break;
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }

            var certKey = KeyFactory.NewKey(KeyAlgorithm.RS256);
            var cert = await order.Generate(new CsrInfo { CommonName = domain }, certKey);
            return (cert.ToPem(), certKey.ToPem());
        }
        finally
        {
            // 清理 TXT 记录
            try { await dns.DeleteTxtRecordAsync(domain, txtValue, ct); }
            catch (Exception ex) { _logger.LogWarning("清理 TXT 记录失败: {Error}", ex.Message); }
        }
    }
}
