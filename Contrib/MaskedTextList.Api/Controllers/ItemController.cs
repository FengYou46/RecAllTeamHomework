using Microsoft.AspNetCore.Mvc;
using RecAll.Contrib.MaskedTextList.Api.Commands;
using RecAll.Contrib.MaskedTextList.Api.Models;
using RecAll.Contrib.MaskedTextList.Api.Services;

namespace RecAll.Contrib.MaskedTextList.Api.Controllers; 

[ApiController]
[Route("[controller]")]
public class ItemController {
    private readonly MaskedTextListContext _maskedTextListContext;
    private readonly IIdentityService _identityService;

    public ItemController(MaskedTextListContext maskedTextListContext, IIdentityService identityService) {
        _maskedTextListContext = maskedTextListContext;
        _identityService = identityService;
    }

    [Route("create")]
    [HttpPost]
    public async Task<ActionResult<string>> CreateAsync(
        [FromBody] CreateMaskedTextItemCommand command) {

        var maskedTextItem = new MaskedTextItem() {
            Content = command.Content,
            MaskedContent = command.MaskedContent,
            UserIdentityGuid = _identityService.GetUserIdentityGuid(),
            IsDeleted = false
        };

        var maskedTextItemEntity = _maskedTextListContext.Add(maskedTextItem);
        await _maskedTextListContext.SaveChangesAsync();

        return maskedTextItemEntity.Entity.Id.ToString();
    }
}