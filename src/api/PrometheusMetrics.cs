using Prometheus;

static class PrometheusMetrics
{
    public static readonly Counter ForecastProcessing = Metrics
     .CreateCounter(
         name: "api_forecast_processing",
         help: "Number of forecasts processed");
}