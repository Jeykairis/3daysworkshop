using api;
using Microsoft.EntityFrameworkCore;
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

var app = builder.Build();

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger/"));
// }
app.UseDeveloperExceptionPage();
// app.UseHttpsRedirection();

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
