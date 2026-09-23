// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Admin.NET.Application;

/// <summary>
/// 商户 Webhook：入队 + 派发（OrderResult）
/// </summary>
/// <remarks>
/// 成功条件：HTTP 2xx 且正文 trim 后等于 success（忽略大小写）。本期不签名。
/// 重试间隔约 30s / 2m / 10m / 30m / 1h / 2h / 4h，耗尽后标 Dead。
/// </remarks>
public class PayWebhookService : ITransient
{
    /// <summary>
    /// 第 N 次失败后的等待秒数（AttemptCount 从 1 起计）
    /// </summary>
    private static readonly int[] RetryDelaysSeconds =
    [
        30, 120, 600, 1800, 3600, 7200, 14400
    ];

    private static readonly JsonSerializerSettings PayloadJsonSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        DateFormatString = "yyyy-MM-dd HH:mm:ss",
        NullValueHandling = NullValueHandling.Ignore
    };

    private readonly SqlSugarRepository<PayWebhookOutbox> _outboxRep;
    private readonly SqlSugarRepository<SysOpenAccess> _openAccessRep;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PayWebhookService> _logger;

    public PayWebhookService(SqlSugarRepository<PayWebhookOutbox> outboxRep,
        SqlSugarRepository<SysOpenAccess> openAccessRep,
        IHttpClientFactory httpClientFactory,
        ILogger<PayWebhookService> logger)
    {
        _outboxRep = outboxRep;
        _openAccessRep = openAccessRep;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// 订单终态入队（无 WebhookUrl / 无 ClientId 则跳过；eventId 冲突视为已入队）
    /// </summary>
    [NonAction]
    public async Task EnqueueOrderResultAsync(PayOrder order)
    {
        if (order == null || order.ClientId is null or <= 0) return;
        if (order.Status != PayOrderStatusEnum.Completed && order.Status != PayOrderStatusEnum.Expired) return;

        var access = await _openAccessRep.GetByIdAsync(order.ClientId.Value);
        var url = access?.WebhookUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url)) return;

        var statusName = order.Status.ToString();
        var eventId = $"order:{order.Id}:result:{statusName}";

        if (await _outboxRep.AsQueryable().AnyAsync(u => u.EventId == eventId)) return;

        var payload = new
        {
            eventId,
            eventType = "OrderResult",
            orderNo = order.OrderNo,
            externalNo = order.ExternalNo,
            status = statusName,
            requestAmount = FormatAmount(order.RequestAmount),
            receivedAmount = FormatAmount(order.ReceivedAmount),
            completeTime = order.CompleteTime
        };

        var now = DateTime.Now;
        try
        {
            await _outboxRep.InsertAsync(new PayWebhookOutbox
            {
                EventId = eventId,
                EventType = "OrderResult",
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                ClientId = order.ClientId.Value,
                WebhookUrl = url.Length > PayConst.WebhookUrlLength
                    ? url[..PayConst.WebhookUrlLength]
                    : url,
                Payload = JsonConvert.SerializeObject(payload, PayloadJsonSettings),
                Status = PayWebhookStatusEnum.Pending,
                AttemptCount = 0,
                NextRetryTime = now
            });
        }
        catch (Exception ex)
        {
            // 唯一索引冲突 = 并发入队成功；其它异常只记日志，不回滚业务主事务调用方
            _logger.LogWarning(ex, "Webhook 入队失败 eventId={EventId} orderNo={OrderNo}", eventId, order.OrderNo);
        }
    }

    /// <summary>
    /// 消费一批待派发 outbox
    /// </summary>
    [NonAction]
    public async Task<int> DispatchBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0) batchSize = PayConst.WebhookDispatchBatchSize;
        var now = DateTime.Now;

        var rows = await _outboxRep.AsQueryable()
            .Where(u => u.Status == PayWebhookStatusEnum.Pending
                        && (u.NextRetryTime == null || u.NextRetryTime <= now))
            .OrderBy(u => u.NextRetryTime, OrderByType.Asc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var ok = 0;
        foreach (var row in rows)
        {
            try
            {
                if (await TryDispatchOneAsync(row, cancellationToken)) ok++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook 派发异常 outboxId={Id} eventId={EventId}", row.Id, row.EventId);
            }
        }
        return ok;
    }

    private async Task<bool> TryDispatchOneAsync(PayWebhookOutbox row, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(PayConst.WebhookHttpTimeoutSeconds);

        string body;
        int statusCode;
        try
        {
            using var content = new StringContent(row.Payload ?? "", Encoding.UTF8, "application/json");
            using var resp = await client.PostAsync(row.WebhookUrl, content, cancellationToken);
            statusCode = (int)resp.StatusCode;
            body = await resp.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await MarkFailureAsync(row, ex.Message);
            return false;
        }

        var trimmed = (body ?? "").Trim();
        if (statusCode >= 200 && statusCode < 300
            && string.Equals(trimmed, "success", StringComparison.OrdinalIgnoreCase))
        {
            await _outboxRep.AsUpdateable()
                .SetColumns(u => new PayWebhookOutbox
                {
                    Status = PayWebhookStatusEnum.Success,
                    AttemptCount = u.AttemptCount + 1,
                    LastError = null,
                    UpdateTime = DateTime.Now
                })
                .Where(u => u.Id == row.Id && u.Status == PayWebhookStatusEnum.Pending)
                .ExecuteCommandAsync(cancellationToken);
            return true;
        }

        await MarkFailureAsync(row, $"HTTP {statusCode} body={Truncate(trimmed, 200)}");
        return false;
    }

    private async Task MarkFailureAsync(PayWebhookOutbox row, string error)
    {
        var attempt = row.AttemptCount + 1;
        var now = DateTime.Now;
        var dead = attempt >= RetryDelaysSeconds.Length;
        var delayIdx = Math.Min(attempt - 1, RetryDelaysSeconds.Length - 1);
        var next = dead ? (DateTime?)null : now.AddSeconds(RetryDelaysSeconds[delayIdx]);

        await _outboxRep.AsUpdateable()
            .SetColumns(u => new PayWebhookOutbox
            {
                Status = dead ? PayWebhookStatusEnum.Dead : PayWebhookStatusEnum.Pending,
                AttemptCount = attempt,
                NextRetryTime = next,
                LastError = Truncate(error, 512),
                UpdateTime = now
            })
            .Where(u => u.Id == row.Id && u.Status == PayWebhookStatusEnum.Pending)
            .ExecuteCommandAsync();
    }

    private static string FormatAmount(decimal amount)
        => amount.ToString($"F{PayConst.AmountScale}", System.Globalization.CultureInfo.InvariantCulture);

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];
}
