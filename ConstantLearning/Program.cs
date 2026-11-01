using ConstantLearning.Configuration;
using ConstantLearning.Features.Bot;
using ConstantLearning.Features.Content;
using ConstantLearning.Features.Identity;
using ConstantLearning.Features.Learning;
using ConstantLearning.Features.Notifications;
using ConstantLearning.HostedServices;
using ConstantLearning.Jobs;
using ConstantLearning.Platform;
using ConstantLearning.Platform.Metrics;
using ConstantLearning.Services;
using Quartz;
using Telegram.Bot;

namespace ConstantLearning;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);


        builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
        builder.Services.Configure<LearningOptions>(builder.Configuration.GetSection(LearningOptions.SectionName));
        builder.Services.Configure<WordsImportOptions>(
            builder.Configuration.GetSection(WordsImportOptions.SectionName));
        builder.Services.Configure<LanguageOptions>(builder.Configuration.GetSection(LanguageOptions.SectionName));

        builder.Configuration.AddJsonFile("Resources/BotMessages.json", optional: false, reloadOnChange: true);

        builder.Services.AddPlatform(builder.Configuration);
        builder.Services.AddIdentity(builder.Configuration);
        builder.Services.AddLearning();
        builder.Services.AddContent();
        builder.Services.AddNotifications();
        builder.Services.AddBot();

        var telegramOptions = builder
                                  .Configuration
                                  .GetSection(TelegramOptions.SectionName)
                                  .Get<TelegramOptions>()
                              ?? throw new InvalidOperationException("Telegram configuration not found");

        builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(telegramOptions.BotToken));
        builder.Services.AddScoped<ITelegramBotService, TelegramBotService>();

        builder.Services.AddSingleton<IBotMessagesService, BotMessagesService>();
        builder.Services.AddSingleton<ITemplateService, TemplateService>();
        builder.Services.AddScoped<IWordService, WordService>();
        builder.Services.AddScoped<IMessageFormatterService, MessageFormatterService>();
        builder.Services.AddScoped<IWordImportService, WordImportService>();
        builder.Services.AddScoped<IProgressService, ProgressService>();
        builder.Services.AddScoped<IChatRegistrationService, ChatRegistrationService>();

        builder.Services.AddQuartz(q =>
        {

            var repetitionJobKey = new JobKey("RepetitionJob");
            q.AddJob<RepetitionJob>(opts => opts.WithIdentity(repetitionJobKey));

            q.AddTrigger(opts => opts
                .ForJob(repetitionJobKey)
                .WithIdentity("RepetitionJob-trigger")
                .WithCronSchedule("0 * * * * ?"));

            var newWordsJobKey = new JobKey("NewWordsJob");
            q.AddJob<NewWordsJob>(opts => opts.WithIdentity(newWordsJobKey));

            q.AddTrigger(opts => opts
                .ForJob(newWordsJobKey)
                .WithIdentity("NewWordsJob-trigger")
                .WithCronSchedule("0 * * * * ?"));
        });

        builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        builder.Services.AddHostedService<DatabaseInitializationService>();
        builder.Services.AddHostedService<WebhookConfigurationService>();

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseMiddleware<ServerTimingMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapLocalMetrics();

        // Невідомі API-маршрути не повинні повертати HTML оболонки.
        app.MapFallback("/api/{**path}", () => Results.NotFound());
        app.MapFallbackToFile("index.html");

        app.Run();
    }
}
