using Microsoft.AspNetCore.SignalR.Client;
var url = args.Length > 0 ? args[0] : "http://localhost:8080/hub/test";
Console.WriteLine($"Connecting to {url} ...");

var connection = new HubConnectionBuilder()
    .WithUrl(url) // route qua haproxy
    .WithAutomaticReconnect()
    .Build();

connection.On<string, string>("ReceiveMessage", (user, msg) =>
{
    Console.WriteLine($"📩 {user}: {msg}");
});

await connection.StartAsync();
Console.WriteLine("✅ Connected to SignalR hub via HAProxy");

while (true)
{
    var msg = Console.ReadLine();
    if (msg == "exit") break;
    await connection.InvokeAsync("SendMessage", "Client", msg);
}