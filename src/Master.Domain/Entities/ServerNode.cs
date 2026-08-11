namespace Master.Domain.Entities;

public class Server
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string AgentTokenHash { get; set; } = string.Empty; // HMAC 密钥哈希
    public ServerConnectionMode ConnectionMode { get; set; } = ServerConnectionMode.WebSocket;
    public ServerStatus Status { get; set; } = ServerStatus.Installing;
    public DateTime? LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum ServerConnectionMode
{
    WebSocket = 0,
    HttpPoll = 1
}

public enum ServerStatus
{
    Online = 0,
    Offline = 1,
    Installing = 2
}

public class Node
{
    public Guid Id { get; set; }
    public Guid ServerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProxyProtocol Protocol { get; set; } = ProxyProtocol.VLESS;
    public int Port { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public bool Enabled { get; set; } = true;
    public double RateMultiplier { get; set; } = 1.0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum ProxyProtocol
{
    VLESS = 0,
    VMess = 1,
    Trojan = 2,
    Shadowsocks = 3,
    Hysteria2 = 4,
    AnyTLS = 5
}

public class NodeUserBinding
{
    public Guid Id { get; set; }
    public Guid NodeId { get; set; }
    public Guid UserId { get; set; }
    public string PerUserConfigJson { get; set; } = "{}"; // 每用户独立 UUID/密码
}
