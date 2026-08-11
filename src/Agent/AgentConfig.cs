namespace Agent;

public class AgentConfig
{
    public string MasterUrl { get; set; } = string.Empty;
    public string ServerId { get; set; } = string.Empty;
    public string AgentToken { get; set; } = string.Empty;
    public int ReportIntervalSeconds { get; set; } = 15;
    public string XrayPath { get; set; } = "/opt/xray-agent/xray/xray";
    public string XrayConfigPath { get; set; } = "/opt/xray-agent/xray/config.json";
    public string WorkDir { get; set; } = "/opt/xray-agent";
}
