using Autofac;
using RecAll.Contrib.TextList.Api.Controllers;
using RecAll.Infrastructure.EventBus.Abstractions;
using System.Reflection;
using Module = Autofac.Module;

namespace RecAll.Contrib.TextList.Api.AutofacModules;

public class ApplicationModule : Module {
    protected override void Load(ContainerBuilder builder) {
        builder.RegisterAssemblyTypes(typeof(ItemController).GetTypeInfo()
            .Assembly).AsClosedTypesOf(typeof(IIntegrationEventHandler<>));
    }
}