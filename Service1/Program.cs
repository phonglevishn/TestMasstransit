using Example.Contracts;
using Humanizer;
using Humanizer.Configuration;
using MassTransit;
using MassTransit.Clients;
using MassTransit.Internals.GraphValidation;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Service1;
using System;
using System.Threading;
using System.Threading.Tasks;
using IHost = Microsoft.Extensions.Hosting.IHost;

namespace Example
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            using var host = CreateHost();

            await host.RunAsync();
        }

        public static IHost CreateHost()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            return new HostBuilder()
                .ConfigureServices((hostContext, services) => { ConfigureServices(services); })
                .UseConsoleLifetime()
                .UseSerilog()
                .Build();
        }

        static void ConfigureServices(IServiceCollection collection)
        {
            collection.AddMassTransit(x =>
            {
                x.SetRabbitMqReplyToRequestClientFactory();
                x.AddConsumer<DirectResponder>();
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host("localhost", 5672, "/", h =>
                    {
                        h.Username("admin");
                        h.Password("admin");
                    });
                    // Log reconnect và lỗi bus
                    cfg.ConnectBusObserver(new BusLifecycleObserver(collection.BuildServiceProvider().GetRequiredService<ILogger<BusLifecycleObserver>>()));

                    cfg.MessageTopology.SetEntityNameFormatter(new CustomEntityNameFormatter(string.Empty, false));

                    cfg.ReceiveEndpoint("direct-request", e =>
                    {
                        //e.SetQuorumQueue(3);
                        e.PrefetchCount = 1;
                        e.Durable = true;
                        e.ConfigureConsumeTopology = false;
                        e.ConfigureConsumer<DirectResponder>(context);
                        //e.SetQuorumQueue();
                        //Console.ForegroundColor = ConsoleColor.Yellow;
                        //Console.WriteLine("⚙️ Using QUORUM queue");

                        //e.SetQueueArgument("x-ha-policy", "all");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("⚙️ Using CLASSIC queue");
                        Console.ResetColor();
                    });
                    cfg.ConfigureEndpoints(context, new SnakeCaseEndpointNameFormatter(prefix: string.Empty, false));
                }
                );
            });

            // before start create topology like test(exchange)->test(queue)
            // at rabbitmq
            //collection.AddHostedService<Sender>(); 
            // ScopedClientFactory để gửi request
            //collection.AddHostedService<RequestClientService>();
            collection.AddHostedService<HealthCheckWorker>();
        }
    }
    public class HealthCheckWorker : BackgroundService
    {
        private readonly HealthCheckService _healthCheckService;
        private readonly ILogger<HealthCheckWorker> _logger;

        public HealthCheckWorker(HealthCheckService healthCheckService, ILogger<HealthCheckWorker> logger)
        {
            _healthCheckService = healthCheckService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var report = await _healthCheckService.CheckHealthAsync(stoppingToken);
                foreach (var entry in report.Entries)
                {
                    _logger.LogInformation("HealthCheck {Name} => {Status} ({Description})",
                        entry.Key, entry.Value.Status, entry.Value.Description);
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    public class FancyNameFormatter :
    IEntityNameFormatter
    {
        IEntityNameFormatter _original;
        public FancyNameFormatter(IEntityNameFormatter original)
        {
            _original = original;
        }

        public string FormatEntityName<T>()
        {
            return _original.FormatEntityName<T>();
        }
    }
    public class CustomEntityNameFormatter : IEntityNameFormatter
    {
        private readonly string _prefix;
        private readonly bool include_Namespace;

        public CustomEntityNameFormatter(string prefix, bool include_namespace = false)
        {
            _prefix = !string.IsNullOrEmpty(prefix) ? $"{prefix}_" : "";
            include_Namespace = include_namespace;
        }
        public string FormatEntityName<T>()
        {
            return include_Namespace ?
                $"{_prefix}{typeof(T).FullName.Underscore()}"
                : $"{_prefix}{typeof(T).Name.Underscore()}";
        }
        //public string FormatEntityName<T>()
        //{
        //    return new StringBuilder(typeof(T).FullName)
        //        .Replace($".{typeof(T).Name}", string.Empty)
        //        .Append($":{typeof(T).Name}")
        //        .ToString();
        //}
    }
    public class CustomMessageEntityNameFormatter<T> :
        IMessageEntityNameFormatter<T> where T : class
    {
        private readonly string _prefix;
        private readonly bool include_Namespace;

        public CustomMessageEntityNameFormatter(string prefix, bool include_namespace = false)
        {
            _prefix = !string.IsNullOrEmpty(prefix) ? $"{prefix}_" : "";
            include_Namespace = include_namespace;
        }
        public string FormatEntityName()
        {
            return include_Namespace ?
                $"{_prefix}{typeof(T).FullName.Underscore()}"
                : $"{_prefix}{typeof(T).Name.Underscore()}";
        }
        //public string FormatEntityName<T>()
        //{
        //    return new StringBuilder(typeof(T).FullName)
        //        .Replace($".{typeof(T).Name}", string.Empty)
        //        .Append($":{typeof(T).Name}")
        //        .ToString();
        //}
    }
}

public class BusLifecycleObserver : IBusObserver
{
    private readonly ILogger<BusLifecycleObserver> _logger;

    public BusLifecycleObserver(ILogger<BusLifecycleObserver> logger)
    {
        _logger = logger;
    }

    public void PostCreate(IBus bus)
    {
        _logger.LogInformation("🚀 Bus created.");
    }

    public void CreateFaulted(Exception exception)
    {
        _logger.LogError(exception, "❌ Bus creation failed.");
    }

    public Task PreStart(IBus bus)
    {
        _logger.LogInformation("▶️ Starting bus...");
        return Task.CompletedTask;
    }

    public Task PostStart(IBus bus, Task<BusReady> busReady)
    {
        _logger.LogInformation("✅ Bus started successfully.");
        return Task.CompletedTask;
    }

    public Task StartFaulted(IBus bus, Exception exception)
    {
        _logger.LogError(exception, "❌ Bus failed to start.");
        return Task.CompletedTask;
    }

    public Task PreStop(IBus bus)
    {
        _logger.LogWarning("🟡 Stopping bus...");
        return Task.CompletedTask;
    }

    public Task PostStop(IBus bus)
    {
        _logger.LogWarning("🛑 Bus stopped.");
        return Task.CompletedTask;
    }

    public Task StopFaulted(IBus bus, Exception exception)
    {
        _logger.LogError(exception, "❌ Bus failed to stop cleanly.");
        return Task.CompletedTask;
    }
}