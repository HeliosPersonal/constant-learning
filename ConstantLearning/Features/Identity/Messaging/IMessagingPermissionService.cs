namespace ConstantLearning.Features.Identity.Messaging;

public interface IMessagingPermissionService
{
    Task SetCanMessageAsync(long telegramUserId, bool canMessage, CancellationToken cancellationToken);
}
