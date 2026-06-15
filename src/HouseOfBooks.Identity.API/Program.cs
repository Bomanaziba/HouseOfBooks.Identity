
// ═════════════════════════════════════════════════════════════════
// FILE: API/Program.cs
// ═════════════════════════════════════════════════════════════════

using HouseOfBooks.Identity.API.Middleware;
using HouseOfBooks.Identity.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ── Services ─────────────────────────────────────────────────────

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Serialize enums as strings in both directions
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
        o.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "House of Books — Identity API", Version = "v1" });
    o.DescribeAllParametersInCamelCase();
});

// Identity module — all persistence, orchestration, outbox, idempotency
builder.Services.AddIdentityOrchestration(builder.Configuration);

// Redis — only materialised when Identity:UseRedis = true
if (builder.Configuration.GetValue<bool>("Identity:UseRedis"))
{
    builder.Services.AddStackExchangeRedisCache(o =>
    {
        o.Configuration = builder.Configuration
                                 .GetConnectionString("Redis");
    });
}

// ── Pipeline ──────────────────────────────────────────────────────

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();  // outermost — catches everything
app.UseMiddleware<SchoolContextMiddleware>();    // tenant header guard

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();

// Expose Program for WebApplicationFactory in integration tests
public partial class Program { }

