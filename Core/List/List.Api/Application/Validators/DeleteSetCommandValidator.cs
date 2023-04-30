using FluentValidation;
using RecAll.Core.List.Api.Application.Commands;
using RecAll.Core.List.Api.Infrastructure.Services;
using RecAll.Core.List.Domain.AggregateModels.SetAggregate;

namespace RecAll.Core.List.Api.Application.Validators;

public class DeleteSetCommandValidator : AbstractValidator<DeleteSetCommand> {
    public DeleteSetCommandValidator(IIdentityService identityService,
        ISetRepository setRepository,
        ILogger<DeleteSetCommandValidator> logger) {
        RuleFor(p => p.Id).NotEmpty();
        RuleFor(p => p.Id).MustAsync(async (p, _) => {
            var userIdentityGuid = identityService.GetUserIdentityGuid();
            var isValid =
                await setRepository.GetAsync(p, userIdentityGuid) is not null;

            if (!isValid) {
                logger.LogWarning(
                    $"用户{userIdentityGuid}尝试删除已删除、不存在或不属于自己的Set {p}");
            }

            return isValid;
        }).WithMessage("无效的Set ID");
        logger.LogTrace("----- INSTANCE CREATED - {ClassName}", GetType().Name);
    }
}