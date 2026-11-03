namespace ConstantLearning.Features.Identity.ProgressReset;

/// <summary>Підсумок спроби скидання для агрегованого лічильника (без персональних даних, PRD §7).</summary>
public enum ProgressResetOutcomeKind
{
    /// <summary>Були дані навчання і їх стерто.</summary>
    Erased,

    /// <summary>Прогрес уже був порожнім, нічого не змінено (AC-09).</summary>
    Empty,

    /// <summary>Перевірка цілісності після стирання не пройшла — транзакцію відкочено (AC-04, §6).</summary>
    IntegrityFailed,

    /// <summary>Обробник чи база впали посеред скидання — транзакцію відкочено (AC-04).</summary>
    Failed
}

/// <summary>
/// Агрегована телеметрія скидань прогресу (PRD §7): лічильник за підсумком і гістограма тривалості.
/// Мітки не містять <c>learner_id</c> чи Telegram-ідентифікатора.
/// </summary>
public interface IProgressResetMetrics
{
    void RecordReset(ProgressResetOutcomeKind outcome, TimeSpan elapsed);
}
