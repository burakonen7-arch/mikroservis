using Shared.Presentation.ExceptionHandling;
using Stock.Infrastructure;
using Shared.Presentation.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddApiSecurity(builder.Configuration);
builder.Services.AddApiSwagger("Stock API");
builder.Services.AddStockInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Stock API v1");
        options.DocumentTitle = "Stock API";
    });
}
await app.Services.InitializeStockInfrastructureAsync(app.Configuration, app.Lifetime.ApplicationStopping);
app.UseApiSecurity();
app.MapControllers();
app.Run();
