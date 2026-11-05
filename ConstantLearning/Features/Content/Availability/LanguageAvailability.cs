namespace ConstantLearning.Features.Content.Availability;

/// <summary>
/// Чиста логіка прапорця доступності мови (T06, AC-02, <see href="../adr/0004-model-supported-languages-as-config-table.md"/>):
/// мова доступна лише коли її ввімкнено адміністратором (<paramref name="isEnabled"/>) І вона має
/// хоча б одне схвалене опубліковане слово base course (<paramref name="hasApprovedContent"/>).
/// Жодної залежності від БД — композиція обох джерел лишається на боці викликача (T11).
/// </summary>
public static class LanguageAvailability
{
    public static bool IsAvailable(bool isEnabled, bool hasApprovedContent) => isEnabled && hasApprovedContent;
}
