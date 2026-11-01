namespace ConstantLearning.Features.Identity.Messaging;

public interface IMessagingPermissionService
{
    Task<bool?> GetCanMessageAsync(long telegramUserId, CancellationToken cancellationToken);
    Task SetCanMessageAsync(long telegramUserId, bool canMessage, CancellationToken cancellationToken);
}
