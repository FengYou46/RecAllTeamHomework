using RecAll.Infrastructure.EventBus.Events;

namespace RecAll.Contrib.MaskedTextList.Api.IntegrationEvents; 

//消息队列接收消息、保存ItemID时的Handler接口
public record ItemIdAssignedIntegrationEvent(int ItemId, int TypeId,
    string ContribId) : IntegrationEvent;