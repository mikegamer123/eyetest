using EyeExamParser.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Http.Headers;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Logging 
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Memory cache
builder.Services.AddMemoryCache();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("OK"))
    .AddCheck<EyeExamApiHealthCheck>("eyeexamapi_ready", failureStatus: HealthStatus.Unhealthy);

// Parser
builder.Services.AddScoped<IScheduleParser, ScheduleParser>();

// HttpClient configured from appsettings.json
builder.Services.AddHttpClient<IScheduleServices, ScheduleServices>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["EyeExamApi:BaseUrl"];
    var user = config["EyeExamApi:Username"];
    var pass = config["EyeExamApi:Password"];

    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("EyeExamApi:BaseUrl is not configured");

    client.BaseAddress = new Uri(baseUrl);

    // Auth Basic
    var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = r => r.Name == "self"
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Name == "eyeexamapi_ready"
});

app.Run();
