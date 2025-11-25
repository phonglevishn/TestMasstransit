using Example.Contracts;
using Humanizer;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Example
{
    public class RequestClientService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IBusControl _bus;
        private readonly ILogger<RequestClientService> _logger;

        public RequestClientService(
    IBusControl bus, IServiceScopeFactory scopeFactory, ILogger<RequestClientService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _bus = bus;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Starting request client benchmark...");
                var health = _bus.CheckHealth();
                if (health.Status != BusHealthStatus.Healthy)
                {
                    _logger.LogWarning("🟡 Bus not healthy ({status}), waiting to reconnect...", health.Status);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }
                using var scope = _scopeFactory.CreateScope();
                var clientFactory = scope.ServiceProvider.GetRequiredService<IScopedClientFactory>();
                int total = 1;
                var sw = Stopwatch.StartNew();
                var throttler = new SemaphoreSlim(100); // giới hạn 100 concurrent
                var tasks = Enumerable.Range(1, total)
                    .Select(async id =>
                    {
                        await throttler.WaitAsync();
                        try
                        {
                            return await SendRequest(id, clientFactory, stoppingToken);
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    })
                    .ToArray();

                var results = await Task.WhenAll(tasks);

                sw.Stop();
                var success = results.Count(r => !double.IsNaN(r));
                var avg = results.Where(r => !double.IsNaN(r)).DefaultIfEmpty().Average();

                Console.WriteLine($"\n✅ Completed {success}/{total} requests");
                Console.WriteLine($"⏱️ Average RTT: {avg:F1} ms");
                Console.WriteLine($"⚡ Throughput: {total / sw.Elapsed.TotalSeconds:F1} msg/sec");
                _logger.LogInformation("Benchmark completed. Waiting 1 minute before next run...");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            
        }

        private async Task<double> SendRequest(int id, IScopedClientFactory factory, CancellationToken ct)
        {
            var req = new DirectRequest { Id = id, Timestamp = DateTime.UtcNow };
            var sw = Stopwatch.StartNew();

            try
            {
                var currencyServiceAddress = new Uri($"exchange:direct-request");
                var currencyServiceClient = factory.CreateRequestClient<DirectRequest>(currencyServiceAddress, RequestTimeout.After(s: 60));

                var response = await currencyServiceClient.GetResponse<DirectResponse>(req, ct);
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