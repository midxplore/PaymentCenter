// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Admin.NET.Core;

/// <summary>
/// 消费者
/// </summary>
public class RabbitMqConsumer : BackgroundService
{
    private readonly RabbitMqConnection _connection;
    private readonly IMessageHandler _handler;
    private readonly ILogger _logger;

    public RabbitMqConsumer(RabbitMqConnection connection, IMessageHandler handler, ILoggerFactory loggerFactory)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _logger = loggerFactory.CreateLogger(CommonConst.SysLogCategoryName); // 日志过滤标识（会写入数据库）
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await _connection.TryConnectAsync())
        {
            _logger.LogError($"RabbitMQ连接失败，请检查配置文件。");
            return;
        }

        var channel = _connection.Channel;
        if (channel == null)
            throw new InvalidOperationException("RabbitMQ channel is null");

        await channel.QueueDeclareAsync(queue: _handler.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            try
            {
                byte[] body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                await _handler.HandleMessageAsync(message);

                Console.WriteLine($"[x] Received from {_handler.QueueName}: {message}");
                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "消息处理失败");
                await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await channel.BasicConsumeAsync(queue: _handler.QueueName, autoAck: false, consumer: consumer);

        // 注册取消回调
        stoppingToken.Register(async () =>
        {
            _logger.LogInformation("Stopping RabbitMQ consumer");
            if (channel.IsOpen)
            {
                await channel.CloseAsync();
            }
            _connection.Dispose();
        });
    }
}