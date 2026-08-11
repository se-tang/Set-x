using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Master.Domain.Entities;
using Master.Infrastructure;
using Master.Api.Services;

namespace Master.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;

    public AuthController(AppDbContext db, JwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public record RegisterRequest(string Username, string Password, string? Email);
    public record LoginRequest(string Username, string Password);
    public record LoginResponse(string Token, string Username, string Role);

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { success = false, message = "用户名和密码不能为空" });

        var exists = await _db.Users.AnyAsync(u => u.Username == req.Username);
        if (exists)
            return Conflict(new { success = false, message = "用户名已存在" });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Email = req.Email,
            Role = UserRole.User,
            SubscriptionToken = GenerateToken(16)
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "注册成功" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { success = false, message = "用户名或密码错误" });
        if (user.Disabled)
            return Unauthorized(new { success = false, message = "账号已禁用" });

        var token = _jwt.GenerateToken(user.Id, user.Username, user.Role.ToString());
        return Ok(new LoginResponse(token, user.Username, user.Role.ToString()));
    }

    [HttpPost("seed-admin")]
    public async Task<IActionResult> SeedAdmin([FromBody] RegisterRequest req)
    {
        // 仅当系统没有任何管理员时允许
        var hasAdmin = await _db.Users.AnyAsync(u => u.Role == UserRole.Admin);
        if (hasAdmin)
            return Conflict(new { success = false, message = "管理员已存在" });

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Username = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Email = req.Email,
            Role = UserRole.Admin,
            SubscriptionToken = GenerateToken(16)
        };
        _db.Users.Add(admin);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, message = "管理员已创建" });
    }

    private static string GenerateToken(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[RandomNumberGenerator.GetInt32(s.Length)]).ToArray());
    }
}
