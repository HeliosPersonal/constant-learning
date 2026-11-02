using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConstantLearning.Configuration;
using ConstantLearning.Features.Bot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ConstantLearning.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class TelegramController(BotUpdateHandler handler, IOptions<TelegramOptions> options,
    ILogger<TelegramController> logger) : ControllerBase
{
    [HttpPost("webhook", Name = "receiveTelegramUpdate")]
    public async Task<IActionResult> HandleWebhook(CancellationToken cancellationToken)
    {
        var supplied = Request.Headers["X-Telegram-Bot-Api-Secret-Token"];
        var expected = options.Value.SecretToken;
        // Перевіряємо секрет перед читанням JSON, включно з пошкодженим тілом запиту.
        if (string.IsNullOrEmpty(expected) || supplied.Count != 1 || supplied[0] is not { Length: > 0 and <= 256 } token
            || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
                SHA256.HashData(Encoding.UTF8.GetBytes(token))))
        {
            return Unauthorized(new { code = "webhook.invalid_secret", message = "Невірний секрет." });
        }

        try
        {
            var update = await JsonSerializer.DeserializeAsync<Update>(Request.Body, JsonBotAPI.Options, cancellationToken);
            if (update is not null)
            {
                await handler.HandleAsync(update, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Не журналюємо тіло, секрет, персональні поля update чи URL із токеном бота.
            logger.LogError("Помилка обробки Telegram webhook: {ErrorType}", ex.GetType().Name);
        }

        return Ok();
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
}
