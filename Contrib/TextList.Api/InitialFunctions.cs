using RecAll.Contrib.TextList.Api.IntegrationEvents;
using RecAll.Infrastructure.EventBus.Abstractions;
using Serilog;
// using ILogger = Microsoft.Extensions.Logging.ILogger;
using ILogger = Serilog.ILogger;

namespace RecAll.Contrib.TextList.Api;

public class InitialFunctions {
    public static string Namespace = typeof(InitialFunctions).Namespace;
    public static string AppName = Namespace;
    
    public static void ConfigureEventBus(IApplicationBuilder app) {
        var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();

        eventBus
            .Subscribe<ItemIdAssignedIntegrationEvent, IIntegrationEventHandler<
                ItemIdAssignedIntegrationEvent>>();
    }
    
    public static ILogger CreateSerilogLogger(IConfiguration configuration) {
        var seqServerUrl = configuration["Serilog:SeqServerUrl"];
        var logstashUrl = configuration["Serilog:LogstashUrl"];
        var cfg = new LoggerConfiguration().MinimumLevel.Verbose().Enrich
            .WithProperty("ApplicationContext", AppName).Enrich.FromLogContext()
            .WriteTo.Console().WriteTo
            .Seq(string.IsNullOrWhiteSpace(seqServerUrl)
                ? "http://seq"
                : seqServerUrl).WriteTo
            .Http(
                string.IsNullOrWhiteSpace(logstashUrl)
                    ? "http://logstash:8080"
                    : logstashUrl, null).ReadFrom.Configuration(configuration);

        return cfg.CreateLogger();
    }
}