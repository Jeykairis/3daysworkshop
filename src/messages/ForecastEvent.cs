namespace Messages;

public sealed record ForecastEvent(DateTime Date, int TemperatureC, string? Summary);

public sealed record ForecastEvent2(DateTime Date, int TemperatureC, string Summary, string Location);