using Stock.Application;
using Stock.Infrastructure;
using BuildingBlocks;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<StockDbContext>(options => options.UseOracle(builder.Configuration.GetConnectionString("Database")!));
builder.Services.AddScoped<StockDataContext>(sp => sp.GetRequiredService<StockDbContext>());
builder.Services.AddStackExchangeRedisCache(o => { o.Configuration = builder.Configuration.GetConnectionString("Redis"); o.InstanceName = "stock:"; });
builder.Services.AddScoped<ICache, Cache>();
builder.Services.AddScoped<AdjustStockHandler>();
builder.Services.AddScoped<GetStockHandler>();
builder.Services.AddScoped<IMessageHandler, ProductCreatedHandler>();
builder.Services.AddSingleton<RabbitConnection>();
builder.Services.AddHostedService<ConsumerWorker>();
builder.Services.AddHostedService<OutboxWorker<StockDbContext>>();
builder.Services.AddTelemetry("stock-service");
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (ArgumentException ex) { await Results.Problem(statusCode: 400, title: "Invalid request", detail: ex.Message).ExecuteAsync(context); }
    catch (OverflowException) { await Results.Problem(statusCode: 400, title: "Stock quantity overflow").ExecuteAsync(context); }
    catch (DbUpdateConcurrencyException) { await Results.Problem(statusCode: 409, title: "Concurrent update", detail: "Reload the resource and retry with the current version.").ExecuteAsync(context); }
});
// Automatic migrations are opt-in for local development; production uses reviewed schema deployments.
if (builder.Configuration.GetValue<bool>("Database:Initialize"))
{
    using var scope = app.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<StockDbContext>().Database.MigrateAsync();
}
// Declare both durable subscriptions before publishing to prevent loss during first startup.
await using (var channel = await (await app.Services.GetRequiredService<RabbitConnection>().Get(default)).CreateChannelAsync())
{
    await Messaging.Topology(channel, "stock.product-created", Topics.ProductCreated, default);
    await Messaging.Topology(channel, "product.stock-changed", Topics.StockChanged, default);
}
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (StockDbContext db, IDistributedCache redis, RabbitConnection rabbit, CancellationToken ct) =>
{
    try
    {
        if (!await db.Database.CanConnectAsync(ct)) return Results.StatusCode(503);
        await redis.SetStringAsync("health", "ok", new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10) }, ct);
        if (await redis.GetStringAsync("health", ct) != "ok" || !(await rabbit.Get(ct)).IsOpen) return Results.StatusCode(503);
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception) { return Results.StatusCode(503); }
});
app.MapGet("/stocks/{id:guid}", async (Guid id, GetStockHandler handler, CancellationToken ct) => { var s = await handler.Handle(new(id), ct); return s is null ? Results.NotFound() : Results.Ok(s); });
app.MapPost("/stocks/{id:guid}/adjustments", async (Guid id, StockAdjustment request, AdjustStockHandler handler, CancellationToken ct) => { var s = await handler.Handle(new(id, request.Delta, request.ExpectedVersion), ct); return s is null ? Results.NotFound() : Results.Ok(s); });
app.Run();
public sealed record StockAdjustment(int Delta, long ExpectedVersion);
public partial class Program { }
