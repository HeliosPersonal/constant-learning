using ConstantLearning.Data.Entities;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Platform.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Word> Words => Set<Word>();
    public DbSet<LearnedWord> LearnedWords => Set<LearnedWord>();
    public DbSet<ChatRegistration> ChatRegistrations => Set<ChatRegistration>();
    public DbSet<Learner> Learners => Set<Learner>();
    public DbSet<ReminderSchedule> ReminderSchedules => Set<ReminderSchedule>();
    public DbSet<DictionaryEntry> DictionaryEntries => Set<DictionaryEntry>();
    public DbSet<EntryVersion> EntryVersions => Set<EntryVersion>();
    public DbSet<VersionTranslation> VersionTranslations => Set<VersionTranslation>();
    public DbSet<VersionAcceptedAnswer> VersionAcceptedAnswers => Set<VersionAcceptedAnswer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new LearnerConfiguration());
        modelBuilder.ApplyConfiguration(new ReminderScheduleConfiguration());
        modelBuilder.ApplyConfiguration(new DictionaryEntryConfiguration());
        modelBuilder.ApplyConfiguration(new EntryVersionConfiguration());
        modelBuilder.ApplyConfiguration(new VersionTranslationConfiguration());
        modelBuilder.ApplyConfiguration(new VersionAcceptedAnswerConfiguration());

        modelBuilder.Entity<Word>(entity =>
        {
            entity.ToTable("Words");
            entity.HasKey(e => e.Id).HasName("PK_Words");
            entity.HasIndex(e => e.FrequencyRank).HasDatabaseName("IX_Words_FrequencyRank");
            entity.Property(e => e.TargetWord).HasMaxLength(200).IsRequired();
            entity.Property(e => e.SourceMeaning).HasMaxLength(500).IsRequired();
            entity.Property(e => e.PhoneticTranscription).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<LearnedWord>(entity =>
        {
            entity.ToTable("LearnedWords");
            entity.HasKey(e => e.Id).HasName("PK_LearnedWords");
            entity.HasIndex(e => e.WordId).HasDatabaseName("IX_LearnedWords_WordId");
            entity.HasIndex(e => new { e.ChatId, e.WordId }).IsUnique()
                .HasDatabaseName("IX_LearnedWords_ChatId_WordId");
            entity.HasOne(e => e.Word)
                .WithMany()
                .HasForeignKey(e => e.WordId)
                .HasConstraintName("FK_LearnedWords_Words_WordId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatRegistration>(entity =>
        {
            entity.ToTable("ChatRegistrations");
            entity.HasKey(e => e.Id).HasName("PK_ChatRegistrations");
            entity.HasIndex(e => e.ChatId).IsUnique().HasDatabaseName("IX_ChatRegistrations_ChatId");
            entity.Property(e => e.ChatTitle).HasMaxLength(200);
            entity.Property(e => e.RepetitionTime).HasMaxLength(5).IsRequired();
            entity.Property(e => e.NewWordsTime).HasMaxLength(5).IsRequired();
        });

        // До вилучення групової моделі зберігаємо фізичні імена наявної схеми.
        PreserveLegacyColumnNames<Word>(modelBuilder);
        PreserveLegacyColumnNames<LearnedWord>(modelBuilder);
        PreserveLegacyColumnNames<ChatRegistration>(modelBuilder);
    }

    private static void PreserveLegacyColumnNames<TEntity>(ModelBuilder modelBuilder) where TEntity : class
    {
        var entity = modelBuilder.Entity<TEntity>();
        foreach (var property in entity.Metadata.GetProperties())
        {
            entity.Property(property.Name).HasColumnName(property.Name);
        }
    }
}

