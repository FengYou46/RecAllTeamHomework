using RecAll.Core.List.Domain.AggregateModels;
using RecAll.Infrastructure.Ddd.Domain.SeedWork;

namespace RecAll.Core.List.Api.Infrastructure.Services; 

public class ContribUrlService : IContribUrlService {
    public string GetContribUrl(int listTypeId) {
        string route;

        //根据listTypeId路由到不同微服务
        if (listTypeId == ListType.Text.Id) {
            route = "text";
        } else if (listTypeId == ListType.MaskedText.Id) {
            route = "maskedText";
        } else{
            throw new ArgumentOutOfRangeException(nameof(listTypeId),
                $"有效取值为{string.Join(",", Enumeration.GetAll<ListType>().Select(p => p.Id.ToString()))}");
        }

        return $"http://recall-envoygateway/{route}";
    }
}