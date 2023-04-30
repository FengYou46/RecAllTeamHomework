using FluentValidation;
using RecAll.Core.List.Api.Application.Commands;
using RecAll.Core.List.Api.Infrastructure.Services;
using RecAll.Core.List.Domain.AggregateModels.ListAggregate;

namespace RecAll.Core.List.Api.Application.Validators;

public class CreateSetCommandValidator : AbstractValidator<CreateSetCommand> {
    public CreateSetCommandValidator(IIdentityService identityService,
        IListRepository listRepository,
        ILogger<CreateSetCommandValidator> logger) {
        RuleFor(p => p.ListId).NotEmpty();
        RuleFor(p => p.ListId).MustAsync(async (p, _) => {
            var userIdentityGuid = identityService.GetUserIdentityGuid();
            var isValid =
                await listRepository.GetAsync(p, userIdentityGuid) is not null;

            if (!isValid) {
                logger.LogWarning(
                    $"用户{userIdentityGuid}尝试在已删除、不存在或不属于自己的List {p}中创建Set");
            }

            return isValid;
        }).WithMessage("无效的List ID");
        RuleFor(p => p.Name).NotEmpty();
        logger.LogTrace("----- INSTANCE CREATED - {ClassName}", GetType().Name);
    }
}