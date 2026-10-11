using Product.Application;
using Product.Infrastructure;
using BuildingBlocks;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ProductDbContext>(options => options.UseMongoDB(builder.Configuration.GetConnectionString("Database")!, "products"));
builder.Services.AddScoped<ProductDataContext>(sp => sp.GetRequiredService<ProductDbContext>());
builder.Services.AddStackExchangeRedisCache(o => { o.Configuration = builder.Configuration.GetConnectionString("Redis"); o.InstanceName = "product:"; });
builder.Services.AddScoped<ICache, Cache>();
builder.Services.AddScoped<CreateProductHandler>();
builder.Services.AddScoped<GetProductHandler>();
builder.Services.AddScoped<IMessageHandler, StockChangedHandler>();
builder.Services.AddSingleton<RabbitConnection>();
builder.Services.AddHostedService<ConsumerWorker>();
builder.Services.AddHostedService<OutboxWorker<ProductDbContext>>();
builder.Services.AddTelemetry("product-service");
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
// EnsureCreated is intentionally limited to local development; production uses reviewed schema deployments.
if (builder.Configuration.GetValue<bool>("Database:Initialize"))
{
    using var scope = app.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<ProductDbContext>().Database.EnsureCreatedAsync();
}
// Declare both durable subscriptions before publishing to prevent loss during first startup.
await using (var channel = await (await app.Services.GetRequiredService<RabbitConnection>().Get(default)).CreateChannelAsync())
{
    await Messaging.Topology(channel, "stock.product-created", Topics.ProductCreated, default);
    await Messaging.Topology(channel, "product.stock-changed", Topics.StockChanged, default);
}
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (ProductDbContext db, IDistributedCache redis, RabbitConnection rabbit, CancellationToken ct) =>
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
app.MapPost("/products", async (CreateProductCommand command, CreateProductHandler handler, CancellationToken ct) =>
{
    var p = await handler.Handle(command, ct); return Results.Created($"/products/{p.Id}", p);
});
app.MapGet("/products/{id:guid}", async (Guid id, GetProductHandler handler, CancellationToken ct) => { var p = await handler.Handle(new(id), ct); return p is null ? Results.NotFound() : Results.Ok(p); });
app.Run();

public partial class Program { }
