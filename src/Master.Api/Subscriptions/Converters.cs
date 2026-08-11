using System.Text;

namespace Master.Api.Subscriptions;

public interface ISubscriptionConverter
{
    string Format { get; }
    string Convert(IEnumerable<ProxyNodeDto> nodes);
}

public class ClashConverter : ISubscriptionConverter
{
    public string Format => "clash";

    public string Convert(IEnumerable<ProxyNodeDto> nodes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("proxies:");
        foreach (var n in nodes)
        {
            sb.AppendLine($"  - name: {EscapeName(n.Name)}");
            sb.AppendLine($"    type: {ProtocolToClash(n.Protocol)}");
            sb.AppendLine($"    server: {n.Address}");
            sb.AppendLine($"    port: {n.Port}");
            if (n.Protocol == "vless")
            {
                sb.AppendLine($"    uuid: {n.Uuid}");
                if (!string.IsNullOrEmpty(n.Flow))
                    sb.AppendLine($"    flow: {n.Flow}");
            }
            else if (n.Protocol == "trojan")
            {
                sb.AppendLine($"    password: {n.Password}");
            }
            else if (n.Protocol == "ss")
            {
                sb.AppendLine($"    password: {n.Password}");
                sb.AppendLine($"    cipher: {GetExtra(n, "cipher", "aes-128-gcm")}");
            }
            if (n.Tls)
            {
                sb.AppendLine("    tls: true");
                if (!string.IsNullOrEmpty(n.Sni))
                    sb.AppendLine($"    servername: {n.Sni}");
                if (!string.IsNullOrEmpty(n.Fingerprint))
                    sb.AppendLine($"    client-fingerprint: {n.Fingerprint}");
                if (!string.IsNullOrEmpty(n.RealityPublicKey))
                {
                    sb.AppendLine($"    reality-opts:");
                    sb.AppendLine($"      public-key: {n.RealityPublicKey}");
                    sb.AppendLine($"      short-id: {n.RealityShortId}");
                }
            }
            if (n.Network == "ws")
            {
                sb.AppendLine("    network: ws");
                sb.AppendLine("    ws-opts:");
                if (!string.IsNullOrEmpty(n.Path))
                    sb.AppendLine($"      path: {n.Path}");
                if (!string.IsNullOrEmpty(n.Host))
                    sb.AppendLine($"      headers:");
                    sb.AppendLine($"        Host: {n.Host}");
            }
            else if (n.Network == "grpc")
            {
                sb.AppendLine("    network: grpc");
                sb.AppendLine("    grpc-opts:");
                if (!string.IsNullOrEmpty(n.Path))
                    sb.AppendLine($"      grpc-service-name: {n.Path.TrimStart('/')}");
            }
        }
        return sb.ToString();
    }

    private static string ProtocolToClash(string p) => p switch
    {
        "vless" => "vless",
        "trojan" => "trojan",
        "ss" => "ss",
        "vmess" => "vmess",
        _ => p
    };

    private static string GetExtra(ProxyNodeDto n, string key, string def) =>
        n.ExtraParams.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : def;

    private static string EscapeName(string name) =>
        name.Replace(":", "\\:").Replace("\"", "\\\"");
}

public class V2raySubConverter : ISubscriptionConverter
{
    public string Format => "v2ray";

    public string Convert(IEnumerable<ProxyNodeDto> nodes)
    {
        var lines = nodes.Select(VlessLink);
        var joined = string.Join("\n", lines);
        return System.Convert.ToBase64String(Encoding.UTF8.GetBytes(joined));
    }

    private static string VlessLink(ProxyNodeDto n)
    {
        var sb = new StringBuilder();
        sb.Append($"vless://{n.Uuid}@{n.Address}:{n.Port}");
        sb.Append($"?encryption=none");
        if (n.Tls)
        {
            sb.Append("&security=tls");
            if (!string.IsNullOrEmpty(n.Sni)) sb.Append($"&sni={Uri.EscapeDataString(n.Sni)}");
            if (!string.IsNullOrEmpty(n.Fingerprint)) sb.Append($"&fp={n.Fingerprint}");
            if (!string.IsNullOrEmpty(n.RealityPublicKey))
            {
                sb.Append($"&pbk={n.RealityPublicKey}");
                sb.Append($"&sid={n.RealityShortId}");
            }
        }
        if (n.Network == "ws")
        {
            sb.Append("&type=ws");
            if (!string.IsNullOrEmpty(n.Path)) sb.Append($"&path={Uri.EscapeDataString(n.Path)}");
            if (!string.IsNullOrEmpty(n.Host)) sb.Append($"&host={Uri.EscapeDataString(n.Host)}");
        }
        else if (n.Network == "grpc")
        {
            sb.Append("&type=grpc");
            if (!string.IsNullOrEmpty(n.Path)) sb.Append($"&serviceName={Uri.EscapeDataString(n.Path.TrimStart('/'))}");
        }
        if (!string.IsNullOrEmpty(n.Flow)) sb.Append($"&flow={n.Flow}");
        sb.Append($"#{Uri.EscapeDataString(n.Name)}");
        return sb.ToString();
    }
}
