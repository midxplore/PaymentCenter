// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using System.Text.Json;

namespace Admin.NET.Application;

/// <summary>
/// 运营汇总口径纯函数（可单测；勿把 Expired.Amount 当释放额）
/// </summary>
public static class PayOpsMetrics
{
    private static readonly JsonDocumentOptions JsonOpts = new() { AllowTrailingCommas = true };

    /// <summary>
    /// 过期释放回池金额 = 请求 − 已到账（下限 0）
    /// </summary>
    public static decimal ExpireReleased(decimal requestAmount, decimal receivedAmount)
    {
        var released = requestAmount - receivedAmount;
        return released > 0m ? released : 0m;
    }

    /// <summary>
    /// 从审计 Before/After JSON 解析 TotalQuota 差额（投放为正、回收为负）
    /// </summary>
    public static decimal? TryParseTotalQuotaDelta(string beforeJson, string afterJson)
    {
        var before = TryReadTotalQuota(beforeJson);
        var after = TryReadTotalQuota(afterJson);
        if (after == null) return null;
        // AccountAdd：before 无 Total → 视为 0
        return after.Value - (before ?? 0m);
    }

    /// <summary>
    /// 读取快照 JSON 中的 TotalQuota（兼容 Pascal / camel）
    /// </summary>
    public static decimal? TryReadTotalQuota(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json, JsonOpts);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (TryGetDecimal(root, "TotalQuota", out var v) || TryGetDecimal(root, "totalQuota", out v))
                return v;
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 自然日键（本地墙钟日期，进程须 TZ=Asia/Shanghai）
    /// </summary>
    public static DateTime ToBizDate(DateTime localTime) => localTime.Date;

    private static bool TryGetDecimal(JsonElement obj, string name, out decimal value)
    {
        value = 0m;
        if (!obj.TryGetProperty(name, out var el)) return false;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out value)) return true;
        if (el.ValueKind == JsonValueKind.String && decimal.TryParse(el.GetString(), out value)) return true;
        return false;
    }
}
