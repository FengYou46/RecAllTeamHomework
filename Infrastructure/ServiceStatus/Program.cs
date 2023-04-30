var builder = WebApplication.CreateBuilder(args);

//添加健康检查用户界面，使用内存进行存储
builder.Services.AddHealthChecksUI().AddInMemoryStorage();

var app = builder.Build();

//将healthcheckui路径启用
app.UseRouting().UseEndpoints(config => config.MapHealthChecksUI());

app.Run();