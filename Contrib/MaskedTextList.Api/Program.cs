using System.Net;
using System.Reflection;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using RecAll.Contrib.MaskedTextList.Api;
using RecAll.Contrib.MaskedTextList.Api.Services;
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

    //配置跨域
    builder.Services.AddCors(options => {
        options.AddPolicy("CorsPolicy",
            builder => builder.SetIsOriginAllowed(host => true).AllowAnyMethod()
                .AllowAnyHeader().AllowCredentials());
    });

    //启动Controller，配置json序列化（配置json序列化未用到）
    builder.Services.AddControllers().AddJsonOptions(options =>
        options.JsonSerializerOptions.IncludeFields = true);
    //涉及到swagger调试调用
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    //注册流水线，如果是开发模式则启动swagger
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
        endpoints.MapControllers();
        // endpoints.MapHealthChecks("/hc",
        //     new HealthCheckOptions {
        //         Predicate = _ => true,
        //         ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        //     });
        // endpoints.MapHealthChecks("/liveness",
        //     new HealthCheckOptions {
        //         Predicate = r => r.Name.Contains("self")
        //     });
    });

    //创建数据库
    var maskedTextContext = app.Services.CreateScope().ServiceProvider
        .GetService<MaskedTextListContext>();
    maskedTextContext!.Database.Migrate();

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