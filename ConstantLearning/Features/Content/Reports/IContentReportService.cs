namespace ConstantLearning.Features.Content.Reports;

public interface IContentReportService
{
    /// <summary>
    /// Створює один answer_dispute на власну оцінену відповідь у транзакції викликувача.
    /// Вікно доступності й нейтралізацію відповіді перевіряє та зберігає викликувач.
    /// Не застосовує денний ліміт звичайних reports і не створює verification job.
    /// Повертає null для відсутньої або чужої відповіді чи незавершеного learner.
    /// </summary>
    Task<Guid?> CreateAnswerDisputeAsync(Guid learnerId, Guid answerEventId, CancellationToken cancellationToken = default);
}
