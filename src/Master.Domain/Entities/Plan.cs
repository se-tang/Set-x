namespace Master.Domain.Entities;

public class Plan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long TrafficLimitBytes { get; set; }
    public int SpeedLimitMbps { get; set; } // 0 = 不限速
    public int DurationDays { get; set; }
    public decimal Price { get; set; }
}

public class UserPlan
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime ExpireAt { get; set; }
    public long UsedTrafficBytes { get; set; }
    public bool Active { get; set; } = true;

    // 导航属性
    public User? User { get; set; }
    public Plan? Plan { get; set; }
}
