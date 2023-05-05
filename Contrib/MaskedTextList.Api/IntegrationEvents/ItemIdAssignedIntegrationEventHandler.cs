using Microsoft.EntityFrameworkCore;
using RecAll.Contrib.MaskedTextList.Api.Services;
using RecAll.Core.List.Domain.AggregateModels;
using RecAll.Infrastructure.EventBus.Abstractions;

namespace RecAll.Contrib.MaskedTextList.Api.IntegrationEvents; 

public class ItemIdAssignedIntegrationEventHandler :
    IIntegrationEventHandler<ItemIdAssignedIntegrationEvent> {
    private readonly MaskedTextListContext _maskedTextListContext;
    private readonly ILogger<ItemIdAssignedIntegrationEventHandler> _logger;

    public ItemIdAssignedIntegrationEventHandler(
        MaskedTextListContext maskedTextListContext,
        ILogger<ItemIdAssignedIntegrationEventHandler> logger) {
        _maskedTextListContext = maskedTextListContext ??
            throw new ArgumentNullException(nameof(maskedTextListContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ItemIdAssignedIntegrationEvent @event) {
        if (@event.TypeId != ListType.MaskedText.Id) {
            return;
        }

        _logger.LogInformation(
            "----- Handling integration event: {IntegrationEventId} at {AppName} - ({@IntegrationEvent})",
            @event.Id, InitialFunctions.AppName, @event);

        var maskedTextItem = await _maskedTextListContext.MaskedTextItems.FirstOrDefaultAsync(p =>
            p.Id == int.Parse(@event.ContribId));

        if (maskedTextItem is null) {
            _logger.LogWarning("Unknown maskedTextItem id: {ItemId}", @event.ItemId);
            return;
        }

        maskedTextItem.ItemId = @event.ItemId;
        await _maskedTextListContext.SaveChangesAsync();

        _logger.LogInformation(
            "----- Integration event handled: {IntegrationEventId} at {AppName} - ({@IntegrationEvent})",
            @event.Id, InitialFunctions.AppName, @event);
    }
}