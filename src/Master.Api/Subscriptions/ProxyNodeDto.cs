namespace Master.Api.Subscriptions;

public class ProxyNodeDto
{
    public string Name { get; set; } = string.Empty;
    public string Protocol { get; set; } = "vless";
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Uuid { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Network { get; set; } = "tcp";
    public string Path { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public bool Tls { get; set; }
    public string Sni { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = "chrome";
    public string RealityPublicKey { get; set; } = string.Empty;
    public string RealityShortId { get; set; } = string.Empty;
    public string Flow { get; set; } = string.Empty;
    public Dictionary<string, string> ExtraParams { get; set; } = new();
}
