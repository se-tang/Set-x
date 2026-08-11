using Agent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

var configPath = Path.Combine(AppContext.BaseDirectory, "config.yaml");
if (!File.Exists(configPath))
    configPath = "/opt/xray-agent/config.yaml";

AgentConfig agentConfig;
if (File.Exists(configPath))
{
    var yaml = File.ReadAllText(configPath);
    var deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();
    agentConfig = deserializer.Deserialize<AgentConfig>(yaml);
}
else
{
    agentConfig = new AgentConfig();
    Console.WriteLine("⚠️ 未找到 config.yaml，使用默认配置（无法连接主控）");
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton(agentConfig);
builder.Services.AddSingleton<XrayProcessManager>();
builder.Services.AddSingleton<XrayStatsClient>();
builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();
host.Run();
