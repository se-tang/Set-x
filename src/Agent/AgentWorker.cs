using System.Diagnostics;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent;

public class AgentWorker : BackgroundService
{
    private readonly AgentConfig _config;
    private readonly XrayProcessManager _xray;
    private readonly XrayStatsClient _stats;
    private readonly ILogger<AgentWorker> _logger;
    private HubConnection? _hub;

    public AgentWorker(AgentConfig config, XrayProcessManager xray, XrayStatsClient stats, ILogger<AgentWorker> logger)
    {
        _config = config;
        _xray = xray;
        _stats = stats;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 先启动本地 Xray（如果有配置）
        if (File.Exists(_config.XrayConfigPath))
            _xray.Start();

        if (string.IsNullOrEmpty(_config.MasterUrl) || string.IsNullOrEmpty(_config.AgentToken))
        {
            _logger.LogWarning("主控地址或 Token 未配置，仅本地 Xray 模式");
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        await ConnectWithRetryAsync(stoppingToken);
    }

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        var delay = 1;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _hub = new HubConnectionBuilder()
                    .WithUrl(new Uri(new Uri(_config.MasterUrl), "/hubs/agent"), o =>
                    {
                        o.Headers["X-Server-Id"] = _config.ServerId;
                        o.Headers["X-Timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                        o.Headers["X-Signature"] = Sign(_config.ServerId, _config.AgentToken);
                    })
                    .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60) })
                    .Build();

                RegisterHandlers();
                await _hub.StartAsync(ct);
                _logger.LogInformation("已连接主控 {Url}", _config.MasterUrl);
                delay = 1;

                // 定期上报状态 + 流量增量
                await ReportLoopAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning("连接失败（{Delay}s 后重试）: {Error}", delay, ex.Message);
                try { await Task.Delay(TimeSpan.FromSeconds(delay), ct); } catch { break; }
                delay = Math.Min(delay * 2, 60);
            }
        }
    }

    private void RegisterHandlers()
    {
        if (_hub == null) return;

        _hub.On<string>("UpdateXrayConfig", async configJson =>
        {
            _logger.LogInformation("收到配置下发");
            var ok = await _xray.ApplyConfigAsync(configJson);
            await _hub!.InvokeAsync("ReportConfigApplyResult", Guid.Empty, ok, ok ? null : "config test failed");
        });

        _hub.On("RestartXray", () =>
        {
            _xray.Stop();
            _xray.Start();
        });

        _hub.On("StopXray", () => _xray.Stop());
    }

    private async Task ReportStatusAsync(CancellationToken ct)
    {
        if (_hub == null || _hub.State != HubConnectionState.Connected) return;
        try
        {
            await _hub.InvokeAsync("ReportStatus", new
            {
                serverId = _config.ServerId,
                xrayAlive = _xray.IsRunning,
                cpuPercent = GetCpuPercent(),
                memMb = GetMemMb(),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("状态上报失败: {Error}", ex.Message);
        }
    }

    private async Task ReportLoopAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, _config.ReportIntervalSeconds));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_hub != null && _hub.State == HubConnectionState.Connected)
                {
                    var (up, down) = await _stats.CollectDeltaAsync(ct);
                    await _hub.InvokeAsync("ReportStatus", new
                    {
                        serverId = _config.ServerId,
                        xrayAlive = _xray.IsRunning,
                        cpuPercent = GetCpuPercent(),
                        memMb = GetMemMb(),
                        uploadBytes = up,
                        downloadBytes = down,
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    }, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("状态上报失败: {Error}", ex.Message);
            }
            await Task.Delay(interval, ct);
        }
    }

    private static string Sign(string serverId, string token)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var payload = $"{serverId}|{ts}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(token));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static int GetCpuPercent()
    {
        try
        {
            var line = File.ReadLines("/proc/stat").FirstOrDefault(l => l.StartsWith("cpu "));
            if (line == null) return 0;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(long.Parse).ToArray();
            var idle = parts[3];
            var total = parts.Sum();
            if (total == 0) return 0;
            return (int)(100 * (1 - (double)idle / total));
        }
        catch { return 0; }
    }

    private static int GetMemMb()
    {
        try
        {
            var info = File.ReadLines("/proc/meminfo").First(l => l.StartsWith("MemAvailable"));
            var kb = long.Parse(info.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]);
            return (int)(kb / 1024);
        }
        catch { return 0; }
    }
}
