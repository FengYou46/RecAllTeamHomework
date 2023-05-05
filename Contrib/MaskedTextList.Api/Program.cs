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
using RecAll.Contrib.MaskedTextList.Api;
using RecAll.Contrib.MaskedTextList.Api.AutofacModules;
using RecAll.Contrib.MaskedTextList.Api.Services;
using RecAll.Infrastructure;
using RecAll.Infrastructure.Api;
using RecAll.Infrastructure.EventBus;
using RecAll.Infrastructure.EventBus.Abstractions;
using RecAll.Infrastructure.EventBus.RabbitMQ;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
Log.Logger = InitialFunctions.CreateSerilogLogger(builder.Configuration);

try {
    //表示不捕获异常，如果出错则将异常抛出
    builder.WebHost.CaptureStartupErrors(false).ConfigureKestrel(options => {
        //打开81端口处理http2请求
        options.Listen(IPAddress.Any, 81,
            listenOptions => {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        //打开80端口处理http1与http2请求
        options.Listen(IPAddress.Any, 80,
            listenOptions => {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
            });
    });
    
    //启用Autofac
    builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
    builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder => {
        containerBuilder.RegisterModule(new ApplicationModule());
    });
    
    //启用Serilog，将Serilog加入到依赖注入容器中
    builder.Host.UseSerilog();
    
    //??可能是依赖注入DbContext
    builder.Services.AddDbContext<MaskedTextListContext>(options => {
        options.UseSqlServer(builder.Configuration["MaskedTextListContext"],
            sqlServerOptionsAction => {
                sqlServerOptionsAction.MigrationsAssembly(
                    typeof(InitialFunctions).GetTypeInfo().Assembly.GetName()
                        .Name);
                sqlServerOptionsAction.EnableRetryOnFailure(15,
                    TimeSpan.FromSeconds(30), null);
            });
    });

    //依赖注入：IIdentityService
    builder.Services.AddTransient<IIdentityService, MockIdentityService>();
    
    //配置消息总线
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

    //配置跨域
    builder.Services.AddCors(options => {
        options.AddPolicy("CorsPolicy",
            builder => builder.SetIsOriginAllowed(host => true).AllowAnyMethod()
                .AllowAnyHeader().AllowCredentials());
    });

    //启动Controller，配置json序列化（配置json序列化未用到）
    builder.Services.AddControllers().AddJsonOptions(options =>
        options.JsonSerializerOptions.IncludeFields = true);
    //涉及到Swagger调试调用
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    //开启模型验证，如Required特性标记，需要在Controller上开启ApiController
    builder.Services.AddOptions().Configure<ApiBehaviorOptions>(options => {
        options.InvalidModelStateResponseFactory = context =>
            new OkObjectResult(ServiceResult.CreateInvalidParameterResult(
                    new ValidationProblemDetails(context.ModelState).Errors
                        .Select(
                            p => $"{p.Key}: {string.Join(" / ", p.Value)}"))
                .ToServiceResultViewModel());
    });
    
    //引入消息总线
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
    
    //启用了对该项目的健康检查功能，同时将所使用的数据库作为依赖项进行健康检查
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy())
        .AddSqlServer(
            builder.Configuration["MaskedTextListContext"]
            , name: "MaskedTextListDb-check"
            , tags: new[] { "MaskedTextListDb" });
    
    var app = builder.Build();

    //注册流水线，如果是开发模式则启动Swagger
    if (app.Environment.IsDevelopment()) {
        app.UseSwagger();
        app.UseSwaggerUI();
    } else {
        app.UseExceptionHandler("/Error");
    }

    //配置跨域并启动路由
    app.UseCors("CorsPolicy");
    app.UseRouting();

    //将用户输入的网址定向到特定的Controller
    app.UseEndpoints(endpoints => {
        endpoints.MapDefaultControllerRoute();
        //自动将Controller的访问地址进行映射，即ItemController上面的[controller]
        endpoints.MapControllers();
        
        //映射健康检查路径
        endpoints.MapHealthChecks("/hc",
            new HealthCheckOptions {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });
        
        //如果健康则返回"Healthy"
        endpoints.MapHealthChecks("/liveness",
            new HealthCheckOptions {
                Predicate = r => r.Name.Contains("self")
            });
    });

    //创建数据库
    var maskedTextContext = app.Services.CreateScope().ServiceProvider
        .GetService<MaskedTextListContext>();
    maskedTextContext!.Database.Migrate();
    
    //启用消息总线
    InitialFunctions.ConfigureEventBus(app);

    app.Run();
    
    return 0;
} catch (Exception e) {
    //Fatal为日志级别
    Log.Fatal(e, "Program terminated unexpectedly ({ApplicationContext})!",
        InitialFunctions.AppName);
    
    //return表示程序崩溃，如进程不正常
    return 1;
} finally {
    Log.CloseAndFlush();
}