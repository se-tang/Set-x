using System.Text.Json;
using Master.Domain.Entities;

namespace Master.Api.Services;

/// <summary>
/// 生成 Xray 完整配置（inbounds + api + policy stats + outbounds）
/// </summary>
public static class XrayConfigBuilder
{
    public static string Build(Server server, List<Node> nodes)
    {
        var inbounds = new List<object>();
        var apiPort = 46736;

        // stats API 入站
        inbounds.Add(new Dictionary<string, object?>
        {
            ["tag"] = "api",
            ["listen"] = "127.0.0.1",
            ["port"] = apiPort,
            ["protocol"] = "dokodemo-door",
            ["settings"] = new Dictionary<string, object?>
            {
                ["address"] = "127.0.0.1"
            }
        });

        // 每个启用节点一个入站
        foreach (var n in nodes.Where(n => n.Enabled))
        {
            var cfg = ParseConfig(n.ConfigJson);
            var inbound = new Dictionary<string, object?>
            {
                ["tag"] = $"in-{n.Id:N}",
                ["listen"] = "0.0.0.0",
                ["port"] = n.Port,
                ["protocol"] = ProtocolToXray(n.Protocol)
            };

            var settings = new Dictionary<string, object?>();
            switch (n.Protocol)
            {
                case ProxyProtocol.VLESS:
                    settings["clients"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["id"] = Get(cfg, "uuid", Guid.NewGuid().ToString()),
                            ["flow"] = Get(cfg, "flow", "")
                        }
                    };
                    settings["decryption"] = "none";
                    break;
                case ProxyProtocol.VMess:
                    settings["clients"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["id"] = Get(cfg, "uuid", Guid.NewGuid().ToString())
                        }
                    };
                    break;
                case ProxyProtocol.Trojan:
                    settings["clients"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["password"] = Get(cfg, "password", Guid.NewGuid().ToString())
                        }
                    };
                    break;
                case ProxyProtocol.Shadowsocks:
                    settings["method"] = Get(cfg, "method", "aes-128-gcm");
                    settings["password"] = Get(cfg, "password", Guid.NewGuid().ToString());
                    break;
            }
            inbound["settings"] = settings;

            // 流设置
            var network = Get(cfg, "network", "tcp");
            var stream = new Dictionary<string, object?>
            {
                ["network"] = network
            };
            if (network == "ws")
            {
                stream["wsSettings"] = new Dictionary<string, object?>
                {
                    ["path"] = Get(cfg, "path", "/"),
                    ["headers"] = new Dictionary<string, object?>
                    {
                        ["Host"] = Get(cfg, "host", "")
                    }
                };
            }
            else if (network == "grpc")
            {
                stream["grpcSettings"] = new Dictionary<string, object?>
                {
                    ["serviceName"] = Get(cfg, "path", "").TrimStart('/')
                };
            }

            // TLS/Reality
            if (Get(cfg, "tls", "false") == "true")
            {
                var security = new Dictionary<string, object?>
                {
                    ["serverName"] = Get(cfg, "sni", Get(cfg, "host", ""))
                };
                if (Get(cfg, "reality", "false") == "true")
                {
                    stream["security"] = "reality";
                    security["realitySettings"] = new Dictionary<string, object?>
                    {
                        ["show"] = false,
                        ["dest"] = Get(cfg, "dest", "learn.microsoft.com:443"),
                        ["serverNames"] = new[] { Get(cfg, "sni", "learn.microsoft.com") },
                        ["privateKey"] = Get(cfg, "privateKey", ""),
                        ["shortIds"] = new[] { Get(cfg, "sid", "") }
                    };
                }
                else
                {
                    stream["security"] = "tls";
                    security["certificates"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["certificateFile"] = "/opt/xray-agent/certs/fullchain.pem",
                            ["keyFile"] = "/opt/xray-agent/certs/privkey.pem"
                        }
                    };
                }
                stream["tlsSettings"] = security;
            }
            inbound["streamSettings"] = stream;
            inbounds.Add(inbound);
        }

        var config = new Dictionary<string, object?>
        {
            ["log"] = new Dictionary<string, object?>
            {
                ["loglevel"] = "info"
            },
            ["api"] = new Dictionary<string, object?>
            {
                ["tag"] = "api",
                ["services"] = new[] { "StatsService" }
            },
            ["stats"] = new Dictionary<string, object?> { },
            ["policy"] = new Dictionary<string, object?>
            {
                ["levels"] = new Dictionary<string, object?>
                {
                    ["0"] = new Dictionary<string, object?>
                    {
                        ["statsUserUplink"] = true,
                        ["statsUserDownlink"] = true
                    }
                },
                ["system"] = new Dictionary<string, object?>
                {
                    ["statsInboundUplink"] = true,
                    ["statsInboundDownlink"] = true
                }
            },
            ["inbounds"] = inbounds,
            ["outbounds"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["protocol"] = "freedom",
                    ["tag"] = "direct"
                },
                new Dictionary<string, object?>
                {
                    ["protocol"] = "blackhole",
                    ["tag"] = "block"
                }
            },
            ["routing"] = new Dictionary<string, object?>
            {
                ["rules"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["inboundTag"] = new[] { "api" },
                        ["outboundTag"] = "api",
                        ["type"] = "field"
                    }
                }
            }
        };

        return JsonSerializer.Serialize(config);
    }

    private static string ProtocolToXray(ProxyProtocol p) => p switch
    {
        ProxyProtocol.VLESS => "vless",
        ProxyProtocol.VMess => "vmess",
        ProxyProtocol.Trojan => "trojan",
        ProxyProtocol.Shadowsocks => "shadowsocks",
        _ => "vless"
    };

    private static Dictionary<string, string> ParseConfig(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        catch { return new(); }
    }

    private static string Get(Dictionary<string, string> dict, string key, string def) =>
        dict.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : def;
}
