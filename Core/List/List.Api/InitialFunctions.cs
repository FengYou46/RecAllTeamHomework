using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly;
using Serilog;
// using ILogger = Microsoft.Extensions.Logging.ILogger;
using ILogger = Serilog.ILogger;

namespace RecAll.Core.List.Api;

public class InitialFunctions {
    public static string Namespace = typeof(InitialFunctions).Namespace;
    public static string AppName = Namespace;
    
    public static void MigrateDbContext<TContext>(
        IServiceProvider serviceProvider, IConfiguration configuration,
        Action<TContext, IServiceProvider> seeder) where TContext : DbContext {
        var isInKubernetes = IsInKubernetes(configuration);
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var mLogger = services.GetRequiredService<ILogger<TContext>>();
        var context = services.GetService<TContext>();

        try {
            mLogger.LogInformation(
                "Migrating database associated with context {DbContextName}",
                typeof(TContext).Name);
            if (isInKubernetes) {
                InvokeSeeder(seeder, context, services);
            } else {
                var retries = 10;
                var retry = Policy.Handle<SqlException>().WaitAndRetry(retries,
                    retryAttempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, _, retryTime, _) => mLogger.LogWarning(
                        exception,
                        "[{prefix}] Exception {ExceptionType} with message {Message} detected on attempt {retryTime} of {retries}",
                        nameof(TContext), exception.GetType().Name,
                        exception.Message, retryTime, retries));
                retry.Execute(() => InvokeSeeder(seeder, context, services));
            }

            mLogger.LogInformation(
                "finished migrating database associated with context {DbContextName}",
                typeof(TContext).Name);
        } catch (Exception e) {
            mLogger.LogError(e,
                "An error occurred while migrating the database used on context {DbContextName}",
                typeof(TContext).Name);
            if (isInKubernetes) {
                throw;
            }
        }
    }
    
    private static bool IsInKubernetes(IConfiguration configuration) =>
        configuration.GetValue<string>("OrchestratorType")?.ToUpper() == "K8S";

    private static void InvokeSeeder<TContext>(
        Action<TContext, IServiceProvider> seeder, TContext context,
        IServiceProvider serviceProvider) where TContext : DbContext {
        context.Database.Migrate();
        seeder(context, serviceProvider);
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