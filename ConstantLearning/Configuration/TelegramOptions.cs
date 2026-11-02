namespace ConstantLearning.Configuration;

public class TelegramOptions
{
    public const string SectionName = "Telegram";
    
    public required string BotToken { get; set; }
    public string? WebhookUrl { get; set; }
    public string? SecretToken { get; set; }
    public string? MiniAppUrl { get; set; }
}
