using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Features.Identity.Messaging;

internal sealed class MessagingPermissionService(AppDbContext db) : IMessagingPermissionService
{
    public Task<bool?> GetCanMessageAsync(long telegramUserId, CancellationToken cancellationToken) =>
        db.Learners.Where(learner => learner.TelegramUserId == telegramUserId)
            .Select(learner => (bool?)learner.CanMessage).SingleOrDefaultAsync(cancellationToken);

    public async Task SetCanMessageAsync(long telegramUserId, bool canMessage, CancellationToken cancellationToken)
    {
        // Прямий UPDATE не зачіпає налаштувань профілю при конкурентному онбордингу.
        await db.Learners.Where(learner => learner.TelegramUserId == telegramUserId)
            .ExecuteUpdateAsync(update => update.SetProperty(learner => learner.CanMessage, canMessage), cancellationToken);
    }
}
