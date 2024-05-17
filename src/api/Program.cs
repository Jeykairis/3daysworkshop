using api;
using Hangfire;
using Hangfire.PostgreSql;
using Messages;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Logging.ClearProviders();
var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
// Register Serilog
builder.Logging.AddSerilog(logger);

builder.Services.AddDbContext<WeatherForecastContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    logger.Information($"Adding DbContext: {connectionString}");
    options.UseNpgsql(connectionString);
});
builder.Services
    .AddRebus(configure =>
    {
        var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMq");
        return configure
            .Logging(l => l.Serilog())
            .Transport(t => t.UseRabbitMq(rabbitMqConnectionString, "ForecastQueue"))
            .Routing(r => r.TypeBased()
                .Map<ForecastEvent>("ForecastQueue")
                .Map<ForecastEvent2>("ForecastQueue")
            );
    })
    .AddRebusHandler<ForecastHandler>();

builder.Services.AddScoped<IForecastProcessorService, ForecastProcessorService>();

var hangfireConnectionString = builder.Configuration.GetConnectionString("HangfireConnection");
var useHangfire = !string.IsNullOrEmpty(hangfireConnectionString);
if (useHangfire)
{
    logger.Information("Adding Hangfire: " + hangfireConnectionString + ".");

    builder.Services
        .AddHangfire(configure =>
        {
            configure
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(b => b.UseNpgsqlConnection(hangfireConnectionString));
        })
        .AddHangfireServer();
}
var app = builder.Build();

if (useHangfire)
{
    app.UseHangfireDashboard();
}

RecurringJob.AddOrUpdate("hello-world", () => Console.WriteLine("Hello World!"), Cron.Minutely);

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/", () => Results.Redirect("/swagger/"));
// }
app.UseDeveloperExceptionPage();
// app.UseHttpsRedirection();

// app.UseHealthChecks("/health");
app.UseMetricServer("/metrics");

app.MapGet("/forecasts", (WeatherForecastContext context) =>
{
    return context.Forecasts.ToList();
});

app.MapPost("/forecast", (
    WeatherForecastEntity forecast,
    WeatherForecastContext context) =>
{
    context.Forecasts.Add(forecast);
    context.SaveChanges();
    return forecast;
});

app.MapPost("/forecast/publish-random", async (IBus bus) =>
{
    var summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    await bus.Send(new ForecastEvent2(
        DateTime.Now.AddDays(Random.Shared.Next(0, 10)),
        Random.Shared.Next(-20, 55),
        summaries[Random.Shared.Next(summaries.Length)],
        new Bogus.DataSets.Address().City()
    ));
    return "Published";
});

app.MapPost("/forecast/process", () =>
{
    app.Logger.LogInformation("Processing");
    var jobId = Guid.NewGuid().ToString("N");
    BackgroundJob.Schedule<IForecastProcessorService>(svc => svc.ProcessForecastsAsync(jobId), DateTime.Now.AddSeconds(1));
    return $"Enqueued {jobId}";
});

app.MapPost("/forecast/process2", () =>
{
    app.Logger.LogInformation("Processing");
    var jobId = Guid.NewGuid().ToString("N");
    BackgroundJob.Schedule<IForecastProcessorService>(svc => svc.ProcessForecastsAsync2(jobId), DateTime.Now.AddSeconds(1));
    return $"Enqueued {jobId}";
});

if (args.Length > 0)
{
    switch (args[0])
    {
        case "--migrate":
            {
                app.Logger.LogInformation("Migrating database");
                using var scope = app.Services.CreateScope();
                using var context = scope.ServiceProvider.GetService<WeatherForecastContext>();
                context?.Database.Migrate();
                return;
            }
    }
}
else
{
    app.Run();
}
record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
