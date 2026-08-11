using System.Text;
using Master.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 数据库
var connStr = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=master.db";
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(connStr));

// 控制器
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 业务服务
builder.Services.AddScoped<Master.Api.Services.JwtService>();

// SignalR Agent Hub
builder.Services.AddSignalR();

// JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev-secret-key-please-change-in-production-0123456789";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "Set-x",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "Set-x",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// 建库
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<Master.Api.Hubs.AgentHub>("/hubs/agent");

app.Run();
