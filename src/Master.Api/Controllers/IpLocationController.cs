using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Master.Api.Controllers;

[ApiController]
[Route("api/ip-lookup")]
[Authorize(Roles = "Admin")]
public class IpLocationController : ControllerBase
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<IpLocationController> _logger;

    public IpLocationController(IHttpClientFactory httpFactory, ILogger<IpLocationController> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    /// <summary>
    /// IP 转地区（ip-api.com，带超时兜底——查询失败返回空，不阻塞建服务器流程）
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Lookup([FromQuery] string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return BadRequest(new { success = false, message = "缺少 ip 参数" });

        try
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var resp = await client.GetFromJsonAsync<IpApiResult>(
                $"http://ip-api.com/json/{Uri.EscapeDataString(ip.Trim())}?lang=zh-CN&fields=status,country,countryCode,regionName,city");
            if (resp?.Status != "success")
                return Ok(new { success = false, region = "" });

            var city = string.IsNullOrEmpty(resp.City) ? resp.RegionName : resp.City;
            return Ok(new
            {
                success = true,
                region = string.IsNullOrEmpty(city)
                    ? resp.Country
                    : $"{resp.Country} · {city}",
                country = resp.Country,
                countryCode = resp.CountryCode,
                city = resp.City,
                regionName = resp.RegionName
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning("IP 查询失败 {Ip}: {Error}", ip, ex.Message);
            return Ok(new { success = false, region = "" }); // 静默降级
        }
    }

    private record IpApiResult(string? Status, string? Country, string? CountryCode,
        string? RegionName, string? City);
}
