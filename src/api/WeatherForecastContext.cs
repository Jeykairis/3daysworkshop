using Microsoft.EntityFrameworkCore;

namespace api;

class WeatherForecastEntity
{
    public int Id { get; set; }
    public string? Location { get; set; }
    public DateTime Date { get; set; }
    public int TemperatureC { get; set; }
    public string? Summary { get; set; }
}

class WeatherForecastContext : DbContext
{
    public DbSet<WeatherForecastEntity> Forecasts { get; set; }

    public WeatherForecastContext(DbContextOptions<WeatherForecastContext> options)
        : base(options)
    {
    }
}