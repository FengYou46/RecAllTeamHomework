using Autofac;
using RecAll.Contrib.MaskedTextList.Api.Controllers;
using RecAll.Infrastructure.EventBus.Abstractions;
using System.Reflection;
using Module = Autofac.Module;

namespace RecAll.Contrib.MaskedTextList.Api.AutofacModules; 

//跟Handler与IHandler关联有关，自动装配Handler
public class ApplicationModule : Module {
    protected override void Load(ContainerBuilder builder) {
        builder.RegisterAssemblyTypes(typeof(ItemController).GetTypeInfo()
            .Assembly).AsClosedTypesOf(typeof(IIntegrationEventHandler<>));
    }
}