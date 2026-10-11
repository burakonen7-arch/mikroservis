using Shared.Presentation.ExceptionHandling;
using Stock.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddStockInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();
app.UseExceptionHandler();
await app.Services.InitializeStockInfrastructureAsync(app.Configuration, app.Lifetime.ApplicationStopping);
app.MapControllers();
app.Run();
