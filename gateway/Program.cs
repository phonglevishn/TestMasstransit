var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
var app = builder.Build();
app.Use((context, next) =>
{
    Console.WriteLine("Incoming request: {Method} {Path} Headers:{Headers}", context.Request.Method, context.Request.Path, context.Request.Headers.ob);
    return next(context);
});
// Configure the HTTP request pipeline.

//app.UseHttpsRedirection();

app.MapGet("/", () => "YARP Gateway");

app.MapReverseProxy(proxyPipeline =>
{
    // Forward websockets automatically — no extra code needed.
});
app.Run();
