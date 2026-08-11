using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Master.Domain.Entities;
using Master.Infrastructure;
using Master.Api.Subscriptions;

namespace Master.Api.Controllers;

[ApiController]
[Route("sub")]
public class SubscriptionController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly Dictionary<string, ISubscriptionConverter> _converters;

    public SubscriptionController(AppDbContext db)
    {
        _db = db;
        _converters = new Dictionary<string, ISubscriptionConverter>
        {
            ["clash"] = new ClashConverter(),
            ["v2ray"] = new V2raySubConverter(),
            ["singbox"] = new V2raySubConverter() // 占位：后续实现 sing-box JSON
        };
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> Get(string token, [FromQuery] string? client)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.SubscriptionToken == token);
        if (user == null || user.Disabled)
            return NotFound();

        // 客户端类型：参数优先，其次 UA
        var format = client?.ToLower();
        if (string.IsNullOrEmpty(format))
        {
            var ua = Request.Headers.UserAgent.ToString().ToLower();
            if (ua.Contains("clash")) format = "clash";
            else if (ua.Contains("v2ray") || ua.Contains("v2rayn")) format = "v2ray";
            else format = "v2ray";
        }

        if (!_converters.TryGetValue(format, out var converter))
            return BadRequest(new { success = false, message = "不支持的客户端类型" });

        // 查用户绑定节点
        var bindings = await _db.NodeUserBindings
            .Where(b => b.UserId == user.Id)
            .ToListAsync();

        var nodeIds = bindings.Select(b => b.NodeId).ToList();
        var nodes = await _db.Nodes
            .Where(n => nodeIds.Contains(n.Id) && n.Enabled)
            .ToListAsync();

        var dtoList = new List<ProxyNodeDto>();
        foreach (var n in nodes)
        {
            var binding = bindings.First(b => b.NodeId == n.Id);
            var perUser = ParseConfig(binding.PerUserConfigJson);
            var nodeCfg = ParseConfig(n.ConfigJson);
            var server = await _db.Servers.FindAsync(n.ServerId);
            if (server == null) continue;

            dtoList.Add(new ProxyNodeDto
            {
                Name = $"{server.Region}-{n.Name}",
                Protocol = n.Protocol.ToString().ToLower(),
                Address = server.IpAddress,
                Port = n.Port,
                Uuid = Get(perUser, "uuid", Get(nodeCfg, "uuid", Guid.NewGuid().ToString())),
                Password = Get(perUser, "password", Get(nodeCfg, "password", "")),
                Network = Get(nodeCfg, "network", "tcp"),
                Path = Get(nodeCfg, "path", ""),
                Host = Get(nodeCfg, "host", ""),
                Tls = Get(nodeCfg, "tls", "false") == "true",
                Sni = Get(nodeCfg, "sni", ""),
                Fingerprint = Get(nodeCfg, "fingerprint", "chrome"),
                RealityPublicKey = Get(nodeCfg, "pbk", ""),
                RealityShortId = Get(nodeCfg, "sid", ""),
                Flow = Get(nodeCfg, "flow", ""),
            });
        }

        var content = converter.Convert(dtoList);
        return Content(content, format == "clash" ? "text/yaml" : "text/plain");
    }

    private static Dictionary<string, string> ParseConfig(string json)
    {
        try
        {
            var dict = System.Text.Json.JsonSerializer
                .Deserialize<Dictionary<string, string>>(json) ?? new();
            return dict;
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private static string Get(Dictionary<string, string> dict, string key, string def) =>
        dict.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : def;
}
