using System.Diagnostics;
using System.Text.Json;

namespace Agent;

/// <summary>
/// Xray Stats API 采集：通过 `xray api statsquery` CLI 拉取流量计数。
/// 计算增量（识别计数器重置——Agent/Xray 重启后归零）。
/// </summary>
public class XrayStatsClient
{
    private readonly AgentConfig _config;
    private readonly ILogger<XrayStatsClient> _logger;
    private long _lastUplink = -1;
    private long _lastDownlink = -1;

    public XrayStatsClient(AgentConfig config, ILogger<XrayStatsClient> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>采集并返回本次增量（bytes）。无数据时返回 0。</summary>
    public async Task<(long Upload, long Download)> CollectDeltaAsync(CancellationToken ct)
    {
        var (up, down) = await QueryTotalAsync(ct);
        if (up < 0 || down < 0) return (0, 0); // 查询失败

        // 首次：只建立基线
        if (_lastUplink < 0)
        {
            _lastUplink = up;
            _lastDownlink = down;
            return (0, 0);
        }

        // 计数器重置检测：本次 < 上次 → 视为重置，只累加新计数
        var upDelta = up >= _lastUplink ? up - _lastUplink : up;
        var downDelta = down >= _lastDownlink ? down - _lastDownlink : down;

        _lastUplink = up;
        _lastDownlink = down;
        return (upDelta, downDelta);
    }

    private async Task<(long Up, long Down)> QueryTotalAsync(CancellationToken ct)
    {
        try
        {
            // xray api statsquery --server=127.0.0.1:46736 --pattern="inbound>>>" --reset=false
            var psi = new ProcessStartInfo
            {
                FileName = _config.XrayPath,
                Arguments = $"api statsquery --server=127.0.0.1:46736 --pattern=\"inbound>>>traffic>>>\" --reset=false",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            if (p == null) return (-1, -1);
            var stdout = await p.StandardOutput.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            if (p.ExitCode != 0) return (-1, -1);

            return ParseStats(stdout);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("stats 查询失败: {Error}", ex.Message);
            return (-1, -1);
        }
    }

    /// <summary>解析 statsquery 输出（JSON 数组 [{name, value}...]）</summary>
    private static (long Up, long Down) ParseStats(string json)
    {
        long up = 0, down = 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var name = el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var value = el.TryGetProperty("value", out var v) ? v.GetInt64() : 0;
                if (name.Contains("uplink")) up += value;
                else if (name.Contains("downlink")) down += value;
            }
        }
        catch
        {
            // 某些版本输出为文本行 "name: value" 格式
            foreach (var line in json.Split('\n'))
            {
                var idx = line.LastIndexOf(':');
                if (idx <= 0) continue;
                var name = line[..idx];
                if (long.TryParse(line[(idx + 1)..].Trim(), out var val))
                {
                    if (name.Contains("uplink")) up += val;
                    else if (name.Contains("downlink")) down += val;
                }
            }
        }
        return (up, down);
    }
}
