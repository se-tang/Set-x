using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Master.Api.Hubs;

/// <summary>
/// 面向前端浏览器的通知 Hub（JWT 鉴权）——
/// 与 AgentHub（HMAC 鉴权）严格分离，避免两种客户端混用
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // 前端登录后带 JWT 连接——按用户加入分组
        var userId = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                     ?? Context.User?.FindFirst("nameid")?.Value;
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        await base.OnConnectedAsync();
    }
}
