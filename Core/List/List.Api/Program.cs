using System.Data.Common;
using System.Net;
using System.Reflection;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using RecAll.Core.List.Api;
using RecAll.Core.List.Api.Application.IntegrationEvents;
using RecAll.Core.List.Api.Infrastructure;
using RecAll.Core.List.Api.Infrastructure.AutofacModules;
using RecAll.Core.List.Api.Infrastructure.Services;
using RecAll.Core.List.Infrastructure;
using RecAll.Infrastructure;
using RecAll.Infrastructure.Api;
using RecAll.Infrastructure.Api.HttpClient;
using RecAll.Infrastructure.EventBus;
using RecAll.Infrastructure.EventBus.Abstractions;
using RecAll.Infrastructure.EventBus.RabbitMQ;
using RecAll.Infrastructure.IntegrationEventLog;
using RecAll.Infrastructure.IntegrationEventLog.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
Log.Logger = InitialFunctions.CreateSerilogLogger(builder.Configuration);

try {
    builder.WebHost.CaptureStartupErrors(true).ConfigureKestrel(options => {
        options.Listen(IPAddress.Any, 81,
            listenOptions => {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        options.Listen(IPAddress.Any, 80,
            listenOptions => {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
            });
    });

    builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
    builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder => {
        containerBuilder.RegisterModule(new MediatorModule());
        containerBuilder.RegisterModule(new ApplicationModule());
    });

    builder.Host.UseSerilog();

    builder.Services.AddDbContext<ListContext>(options => {
        options.UseSqlServer(builder.Configuration["ListContext"],
            sqlServerOptionsAction => {
                sqlServerOptionsAction.MigrationsAssembly(
                    typeof(InitialFunctions).GetTypeInfo().Assembly.GetName()
                        .Name);
                sqlServerOptionsAction.EnableRetryOnFailure(15,
                    TimeSpan.FromSeconds(30), null);
            });
    });

    builder.Services.AddDbContext<IntegrationEventLogContext>(options => {
        options.UseSqlServer(builder.Configuration["ListContext"],
            sqlServerOptionsAction => {
                sqlServerOptionsAction.MigrationsAssembly(
                    typeof(InitialFunctions).GetTypeInfo().Assembly.GetName()
                        .Name);
                sqlServerOptionsAction.EnableRetryOnFailure(15,
                    TimeSpan.FromSeconds(30), null);
            });
    });

    builder.Services.AddHttpClient(HttpClientFactoryExtension.DefaultClient);
    builder.Services.AddTransient<IIdentityService, MockIdentityService>();
    builder.Services
        .AddTransient<Func<DbConnection, IIntegrationEventLogService>>(_ =>
            connection => new IntegrationEventLogService(connection));
    builder.Services
        .AddTransient<IListIntegrationEventService,
            ListIntegrationEventService>();

    builder.Services.AddSingleton<IRabbitMQConnection>(serviceProvider => {
        var logger = serviceProvider
            .GetRequiredService<ILogger<RabbitMQConnection>>();

        var factory = new ConnectionFactory {
            HostName = builder.Configuration["RabbitMQ"],
            DispatchConsumersAsync = true
        };

        if (!string.IsNullOrWhiteSpace(
                builder.Configuration["RabbitMQUserName"])) {
            factory.UserName = builder.Configuration["RabbitMQUserName"];
        }

        if (!string.IsNullOrWhiteSpace(
                builder.Configuration["RabbitMQPassword"])) {
            factory.Password = builder.Configuration["RabbitMQPassword"];
        }

        var retryCount =
            string.IsNullOrWhiteSpace(
                builder.Configuration["RabbitMQRetryCount"])
                ? 5
                : int.Parse(builder.Configuration["RabbitMQRetryCount"]);

        return new RabbitMQConnection(factory, logger, retryCount);
    });

    builder.Services.AddCors(options => {
        options.AddPolicy("CorsPolicy",
            builder => builder.SetIsOriginAllowed(host => true).AllowAnyMethod()
                .AllowAnyHeader().AllowCredentials());
    });

    builder.Services.AddControllers().AddJsonOptions(options =>
        options.JsonSerializerOptions.IncludeFields = true);
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddOptions().Configure<ApiBehaviorOptions>(options => {
        options.InvalidModelStateResponseFactory = context =>
            new OkObjectResult(ServiceResult.CreateInvalidParameterResult(
                    new ValidationProblemDetails(context.ModelState).Errors
                        .Select(
                            p => $"{p.Key}: {string.Join(" / ", p.Value)}"))
                .ToServiceResultViewModel());
    });

    builder.Services
        .AddSingleton<IEventBusSubscriptionsManager,
            InMemoryEventBusSubscriptionsManager>();
    builder.Services.AddSingleton<IEventBus, RabbitMQEventBus>(
        serviceProvider => new RabbitMQEventBus(
            serviceProvider.GetRequiredService<IRabbitMQConnection>(),
            serviceProvider.GetRequiredService<ILogger<RabbitMQEventBus>>(),
            serviceProvider.GetRequiredService<ILifetimeScope>(),
            serviceProvider.GetRequiredService<IEventBusSubscriptionsManager>(),
            builder.Configuration["EventBusSubscriptionClientName"],
            string.IsNullOrWhiteSpace(
                builder.Configuration["EventBusRetryCount"])
                ? 5
                : int.Parse(builder.Configuration["EventBusRetryCount"])));

    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy()).AddSqlServer(
            builder.Configuration["ListContext"], name: "ListDb-check",
            tags: new[] { "ListDb" });

    var app = builder.Build();

    if (app.Environment.IsDevelopment()) {
        app.UseSwagger();
        app.UseSwaggerUI();
    } else {
        app.UseExceptionHandler("/Error");
    }

    app.UseCors("CorsPolicy");
    app.UseRouting();

    app.UseEndpoints(endpoints => {
        endpoints.MapDefaultControllerRoute();
        endpoints.MapControllers();
        endpoints.MapHealthChecks("/hc",
            new HealthCheckOptions {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });
        endpoints.MapHealthChecks("/liveness",
            new HealthCheckOptions {
                Predicate = r => r.Name.Contains("self")
            });
    });

    InitialFunctions.MigrateDbContext<ListContext>(app.Services,
        builder.Configuration, (context, servicesInside) => {
            var envInside = servicesInside.GetService<IWebHostEnvironment>();
            var loggerInside =
                servicesInside.GetService<ILogger<ListContextSeed>>();
            new ListContextSeed().SeedAsync(context, envInside, loggerInside)
                .Wait();
        });
    InitialFunctions.MigrateDbContext<IntegrationEventLogContext>(app.Services,
        builder.Configuration, (_, _) => { });

    app.Run();
    return 0;
} catch (Exception e) {
    Log.Fatal(e, "Program terminated unexpectedly ({ApplicationContext})!",
        InitialFunctions.AppName);
    return 1;
} finally {
    Log.CloseAndFlush();
}