using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddHealthChecks();
// Redis backplane
var redisConnection = builder.Configuration["REDIS_CONNECTION"] ?? "redis1:6379";
builder.Services.AddSignalR().AddStackExchangeRedis(redisConnection, options =>
{
    options.Configuration.ChannelPrefix = RedisChannel.Literal("signalrapp");
});

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapHub<TestHub>("/hub/test");
app.Run();


public class TestHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}