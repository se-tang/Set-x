namespace Master.Domain.Entities;

public class TrafficRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid NodeId { get; set; }
    public Guid ServerId { get; set; }
    public DateOnly Date { get; set; }
    public long UploadBytes { get; set; }
    public long DownloadBytes { get; set; }
}

public class Certificate
{
    public Guid Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public Guid ServerId { get; set; }
    public string Provider { get; set; } = "LetsEncrypt";
    public string DnsProviderConfigJson { get; set; } = "{}";
    public DateTime ExpireAt { get; set; }
    public DateTime? LastRenewedAt { get; set; }
}

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? OperatorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string? TargetId { get; set; }
    public string? DetailJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
