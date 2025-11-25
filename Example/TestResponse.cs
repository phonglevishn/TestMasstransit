using Example.Contracts;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Example
{
    public class DirectResponderB : IConsumer<DirectRequestB>
    {
        private readonly ILogger<DirectResponderB> _logger;
        public DirectResponderB(ILogger<DirectResponderB> logger) => _logger = logger;

        public async Task Consume(ConsumeContext<DirectRequestB> context)
        {
            await Task.Delay(10); // mô phỏng xử lý 10ms
            _logger.LogInformation($"DirectResponderB consumer message id:{context.Message.Id}, Timestamp:{context.Message.Timestamp}");
            await context.RespondAsync(new DirectResponseB
            {
                Id = context.Message.Id,
                RequestTimestamp = context.Message.Timestamp,
                ResponseTimestamp = DateTime.UtcNow
            });
        }
    }
}