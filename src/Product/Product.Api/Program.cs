using Shared.Presentation.ExceptionHandling;
using Product.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProductInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();
app.UseExceptionHandler();
await app.Services.InitializeProductInfrastructureAsync(app.Configuration, app.Lifetime.ApplicationStopping);
app.MapControllers();
app.Run();
