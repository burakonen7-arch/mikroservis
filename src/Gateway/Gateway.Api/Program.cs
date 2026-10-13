using BuildingBlocks.Observability;
using Shared.Presentation.ExceptionHandling;
using Shared.Presentation.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddApiSecurity(builder.Configuration);
builder.Services.AddApiSwagger("Gateway API");
builder.Services.AddTelemetry("gateway-service");
builder.Services.AddHttpClient("swagger-documents");
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Gateway / Development login");
        options.SwaggerEndpoint("/docs/product/swagger.json", "Product API");
        options.SwaggerEndpoint("/docs/stock/swagger.json", "Stock API");
        options.DocumentTitle = "Mikroservis Gateway";
        options.ConfigObject.PersistAuthorization = true;
    });

    app.MapGet("/docs/{service}/swagger.json", async (string service, IHttpClientFactory clients, IConfiguration configuration, CancellationToken cancellationToken) =>
    {
        var cluster = service switch { "product" => "product", "stock" => "stock", _ => null };
        if (cluster is null) 
            return Results.NotFound();
        
        var address = configuration[$"ReverseProxy:Clusters:{cluster}:Destinations:api:Address"];
        
        if (string.IsNullOrWhiteSpace(address)) 
            return Results.Problem(statusCode: 503, title: "API destination is unavailable");
        
        using var response = await clients.CreateClient("swagger-documents").GetAsync(new Uri(new Uri(address), "swagger/v1/swagger.json"), cancellationToken);
        
        if (!response.IsSuccessStatusCode) 
            return Results.Problem(statusCode: 503, title: "API documentation is unavailable");
        
        return Results.Content(await response.Content.ReadAsStringAsync(cancellationToken), "application/json");
        
    }).AllowAnonymous().DisableRateLimiting();
    
    app.MapGet("/", () => Results.Redirect("/swagger/index.html")).AllowAnonymous().DisableRateLimiting();
}

app.UseApiSecurity();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).AllowAnonymous().DisableRateLimiting();
app.MapControllers();
app.MapReverseProxy().RequireAuthorization().RequireRateLimiting(ApiPolicies.Requests);
app.Run();
