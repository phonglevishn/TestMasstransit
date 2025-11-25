using Example.Contracts;
using MassTransit;
using MassTransit.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Service1
{
    public class DirectResponder : IConsumer<DirectRequest>
    {
        private readonly ILogger<DirectResponder> _logger;
        private readonly IScopedClientFactory _scopeFactory;
        public DirectResponder(ILogger<DirectResponder> logger, IScopedClientFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task Consume(ConsumeContext<DirectRequest> context)
        {
            await Task.Delay(10); // mô phỏng xử lý 10ms
            _logger.LogInformation($"DirectResponder consumer message id:{context.Message.Id}, Timestamp:{context.Message.Timestamp}");
            var t = await SendRequest(context.Message.Id, _scopeFactory, context.CancellationToken);

            await context.RespondAsync(new DirectResponse
            {
                Id = context.Message.Id,
                RequestTimestamp = context.Message.Timestamp,
                ResponseTimestamp = DateTime.UtcNow
            });
        }

        private async Task<double> SendRequest(int id, IScopedClientFactory factory, CancellationToken ct)
        {
            var req = new DirectRequestB { Id = id, Timestamp = DateTime.UtcNow };
            var sw = Stopwatch.StartNew();

            try
            {
                var currencyServiceAddress = new Uri($"exchange:direct-request-b");
                var currencyServiceClient = factory.CreateRequestClient<DirectRequestB>(currencyServiceAddress, RequestTimeout.After(s: 60));

                var response = await currencyServiceClient.GetResponse<DirectResponseB>(req, ct);
                sw.Stop();

                var rtt = sw.Elapsed.TotalMilliseconds;
                return rtt;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Request {id} failed");
                return double.NaN;
            }
        }
    }
}