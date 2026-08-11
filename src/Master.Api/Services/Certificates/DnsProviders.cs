namespace Master.Api.Services.Certificates;

/// <summary>DNS 提供商接口：用于 ACME DNS-01 挑战</summary>
public interface IDnsProvider
{
    Task CreateTxtRecordAsync(string domain, string txtValue, CancellationToken ct);
    Task DeleteTxtRecordAsync(string domain, string txtValue, CancellationToken ct);
}

/// <summary>Cloudflare DNS Provider（API Token 认证）</summary>
public class CloudflareDnsProvider : IDnsProvider
{
    private readonly HttpClient _http;
    private readonly string _apiToken;
    private const string ApiBase = "https://api.cloudflare.com/client/v4";

    public CloudflareDnsProvider(string apiToken)
    {
        _apiToken = apiToken;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");
    }

    public async Task CreateTxtRecordAsync(string domain, string txtValue, CancellationToken ct)
    {
        var (zoneId, recordName) = await ResolveZoneAsync(domain, ct);
        var payload = new
        {
            type = "TXT",
            name = recordName,
            content = txtValue,
            ttl = 120
        };
        var resp = await _http.PostAsJsonAsync($"{ApiBase}/zones/{zoneId}/dns_records", payload, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteTxtRecordAsync(string domain, string txtValue, CancellationToken ct)
    {
        var (zoneId, recordName) = await ResolveZoneAsync(domain, ct);
        var records = await _http.GetFromJsonAsync<CfResponse<CfRecord>>(
            $"{ApiBase}/zones/{zoneId}/dns_records?type=TXT&name={recordName}", ct);
        foreach (var r in records?.Result ?? Array.Empty<CfRecord>())
        {
            if (r.Content == txtValue)
                await _http.DeleteAsync($"{ApiBase}/zones/{zoneId}/dns_records/{r.Id}", ct);
        }
    }

    private async Task<(string ZoneId, string RecordName)> ResolveZoneAsync(string domain, CancellationToken ct)
    {
        // 从最长的域名片段开始匹配 zone
        var parts = domain.Split('.');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var zone = string.Join('.', parts.Skip(i));
            var resp = await _http.GetFromJsonAsync<CfResponse<CfZone>>(
                $"{ApiBase}/zones?name={zone}", ct);
            if (resp?.Result is { Length: > 0 } zones)
            {
                var recordName = i == 0 ? $"_acme-challenge.{domain}"
                    : $"_acme-challenge.{string.Join('.', parts.Take(i))}";
                return (zones[0].Id, recordName);
            }
        }
        throw new InvalidOperationException($"找不到域名 {domain} 对应的 Cloudflare Zone");
    }

    private record CfResponse<T>(bool Success, T[] Result, string[]? Errors);
    private record CfRecord(string Id, string Type, string Name, string Content);
    private record CfZone(string Id, string Name);
}
