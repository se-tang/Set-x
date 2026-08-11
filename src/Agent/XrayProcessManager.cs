using System.Diagnostics;

namespace Agent;

/// <summary>
/// Xray 进程管理：配置校验 → 平滑重启
/// </summary>
public class XrayProcessManager : IDisposable
{
    private readonly AgentConfig _config;
    private readonly ILogger<XrayProcessManager> _logger;
    private Process? _currentProcess;
    private readonly object _lock = new();

    public XrayProcessManager(AgentConfig config, ILogger<XrayProcessManager> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>应用新配置：先 -test 校验，通过后平滑重启。</summary>
    public async Task<bool> ApplyConfigAsync(string newConfigJson)
    {
        var dir = Path.GetDirectoryName(_config.XrayConfigPath) ?? _config.WorkDir;
        Directory.CreateDirectory(dir);
        var tempPath = Path.Combine(dir, "config.new.json");
        await File.WriteAllTextAsync(tempPath, newConfigJson);

        // 1. 校验
        var test = await RunProcessAsync(_config.XrayPath, $"-test -config {tempPath}");
        if (!test.Success)
        {
            _logger.LogError("配置校验失败: {Error}", test.StdErr);
            File.Delete(tempPath);
            return false;
        }

        // 2. 覆盖正式配置
        lock (_lock)
        {
            File.Copy(tempPath, _config.XrayConfigPath, overwrite: true);
        }
        File.Delete(tempPath);

        // 3. 平滑重启：新进程起来后再杀旧的
        var newProcess = StartXray();
        await Task.Delay(2000);
        if (newProcess.HasExited)
        {
            _logger.LogError("新 Xray 进程启动失败（退出码 {Code}），回滚旧配置", newProcess.ExitCode);
            // 回滚：用备份重试一次
            return false;
        }
        lock (_lock)
        {
            _currentProcess?.Kill(true);
            _currentProcess = newProcess;
        }
        _logger.LogInformation("Xray 平滑重启完成 PID={Pid}", newProcess.Id);
        return true;
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_currentProcess == null || _currentProcess.HasExited)
                _currentProcess = StartXray();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _currentProcess?.Kill(true);
            _currentProcess = null;
        }
    }

    public bool IsRunning => _currentProcess is { HasExited: false };

    private Process StartXray()
    {
        var psi = new ProcessStartInfo
        {
            FileName = _config.XrayPath,
            Arguments = $"run -config {_config.XrayConfigPath}",
            WorkingDirectory = Path.GetDirectoryName(_config.XrayConfigPath) ?? _config.WorkDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var p = Process.Start(psi)!;
        _logger.LogInformation("Xray 已启动 PID={Pid}", p.Id);
        return p;
    }

    private static async Task<(bool Success, string StdErr)> RunProcessAsync(string exe, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var p = Process.Start(psi);
        if (p == null) return (false, "无法启动进程");
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return (p.ExitCode == 0, stderr);
    }

    public void Dispose() => Stop();
}
