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
    public ProxyProtocol Protocol { get; set; }
    public int Port { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public bool Enabled { get; set; } = true;
    public double RateMultiplier { get; set; } = 1.0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 部署状态（Step D）
    public NodeDeployStatus DeployStatus { get; set; } = NodeDeployStatus.Pending;
    public string? DeployError { get; set; }
    public DateTime? DeployedAt { get; set; }

    // 导航属性
    public Server? Server { get; set; }
    public ICollection<NodeUserBinding>? Bindings { get; set; }
}

public enum NodeDeployStatus
{
    Pending = 0,   // 未启用/待部署
    Applying = 1,  // 下发中
    Success = 2,   // 已部署成功
    Failed = 3     // 部署失败
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
