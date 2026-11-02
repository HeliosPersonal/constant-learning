using System.Text;
using ConstantLearning.Features.Content.Notifications;
using ConstantLearning.Features.Identity.NotificationAudience;
using ConstantLearning.Features.Notifications.Data;
using ConstantLearning.Features.Notifications.Dispatch;
using NodaTime;

namespace ConstantLearning.Features.Notifications.Summaries;

public sealed class ReportOutcomesHandler(IReportNotificationReader reports, INotificationAudienceReader audience,
    IClock clock) : INotificationHandler
{
    public string Kind => "report_outcomes";

    public async Task<NotificationPreparation> PrepareAsync(NotificationDispatch dispatch, CancellationToken cancellationToken)
    {
        if (dispatch.LearnerId is not { } learnerId || await audience.ReadLearnerAsync(learnerId, cancellationToken) is not { } recipient
            || recipient.TelegramUserId != dispatch.RecipientTelegramUserId)
            return new NotificationPreparation.Skip("summary_recipient_unavailable");
        if (recipient.LocalDate != dispatch.LocalDate) return new NotificationPreparation.Skip("summary_day_changed");
        var due = SummarySchedule.CurrentLearnerDue(recipient);
        if (due is null) return new NotificationPreparation.Skip("summary_time_outside_learner_day");
        var now = clock.GetCurrentInstant();
        if (due > now)
            return dispatch.Attempts == 0 ? new NotificationPreparation.Defer(due.Value)
                : new NotificationPreparation.Skip("summary_timezone_changed_after_attempt");
        if (now > due + NotificationDispatcher.MaximumLateness)
            return new NotificationPreparation.Skip("summary_time_changed_outside_window");
        var batch = await reports.ReadForReservationAsync(learnerId, dispatch.Id, dispatch.DueAt, cancellationToken);
        if (batch.TotalCount == 0) return new NotificationPreparation.Skip("summary_empty");
        var selection = SummaryMessageFormatter.Select(batch);
        if (selection.ReportIds.Count == 0) return new NotificationPreparation.Skip("summary_empty");
        await reports.ReserveAsync(learnerId, dispatch.Id, selection.ReportIds, cancellationToken);
        return new NotificationPreparation.Deliver(new NotificationMessage(selection.Text));
    }
}

internal static class SummaryMessageFormatter
{
    public static SummaryMessageSelection Select(ReportNotificationBatch batch)
    {
        const int maximumLength = 4096;
        // Запас для найбільшого лічильника зберігає той самий префікс під час retry, навіть якщо backlog виріс.
        const int footerBudget = 128;
        var text = new StringBuilder("Результати ваших повідомлень");
        var ids = new List<Guid>();
        foreach (var row in batch.Items)
        {
            var line = $"\n• {Label(row.Label)} ({Context(row)}): {Result(row)}.";
            if (text.Length + line.Length > maximumLength - footerBudget) break;
            text.Append(line);
            ids.Add(row.Id);
        }
        var remaining = batch.TotalCount - ids.Count;
        if (remaining > 0) text.Append($"\n\nВідкладено результатів: {remaining}. Вони будуть у наступному підсумку.");
        return new(ids.AsReadOnly(), text.ToString());
    }

    private static string Label(string value)
    {
        var clean = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var runes = clean.EnumerateRunes().ToArray();
        return runes.Length <= 64 ? clean : string.Concat(runes.Take(63).Select(rune => rune.ToString())) + "…";
    }

    private static string Result(ReportNotificationOutcome row) => row.Outcome switch
    {
        "applied" => "Виправлено",
        "added_to_candidates" => "Додано до кандидатів",
        _ => "Не підтверджено: " + (row.RejectionReason switch
        {
            "source_not_confirming" => "джерело не підтверджує",
            "already_correct" => "запис уже правильний",
            "out_of_scope" => "поза межами курсу",
            "duplicate" => "дублікат",
            "already_in_dictionary" => "уже є у словнику",
            "phrase_not_lemma" => "фраза, а не лема",
            _ => "причину не зазначено"
        })
    };

    private static string Context(ReportNotificationOutcome row) => row.Origin switch
    {
        "missing_word" => "відсутнє слово",
        "answer_dispute" => "варіант відповіді",
        _ => row.Field switch
        {
            "translation" => "переклад",
            "pronunciation" => "вимова",
            "lemma" => "написання",
            "example" => "приклад",
            _ => "запис словника"
        }
    };
}

internal sealed record SummaryMessageSelection(IReadOnlyList<Guid> ReportIds, string Text);
