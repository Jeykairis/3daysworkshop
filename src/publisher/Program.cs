using Messages;
using Microsoft.Extensions.DependencyInjection;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Routing.TypeBased;

var services = new ServiceCollection();
services.AddRebus(c => c
    .Logging(l => l.Serilog())
    .Transport(t => t.UseRabbitMq("amqp://localhost", "ForecastQueue"))
    .Routing(r => r.TypeBased().Map<ForecastEvent>("ForecastQueue"))
);

using var provider = services.BuildServiceProvider();
provider.StartRebus();

var bus = provider.GetService<IBus>()!;

while (true)
{
    await bus.Send(new ForecastEvent(DateTime.Now, 11, "Test"));
    Console.WriteLine("Event published");

    await Task.Delay(1000);
}