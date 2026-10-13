using Shared.Presentation.ExceptionHandling;
using Product.Infrastructure;
using Shared.Presentation.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddApiSecurity(builder.Configuration);
builder.Services.AddApiSwagger("Product API");
builder.Services.AddProductInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Product API v1");
        options.DocumentTitle = "Product API";
    });
}
await app.Services.InitializeProductInfrastructureAsync(app.Configuration, app.Lifetime.ApplicationStopping);
app.UseApiSecurity();
app.MapControllers();
app.Run();
