using ConstantLearning.Data.Entities;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
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
    public DbSet<LearnerWord> LearnerWords => Set<LearnerWord>();
    public DbSet<LearningSession> LearningSessions => Set<LearningSession>();
    public DbSet<SessionItem> SessionItems => Set<SessionItem>();
    public DbSet<AnswerEvent> AnswerEvents => Set<AnswerEvent>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();
    public DbSet<AnswerNeutralization> AnswerNeutralizations => Set<AnswerNeutralization>();
    public DbSet<ValidationEvidence> ValidationEvidence => Set<ValidationEvidence>();
    public DbSet<PublicationLog> PublicationLog => Set<PublicationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new LearnerConfiguration());
        modelBuilder.ApplyConfiguration(new ReminderScheduleConfiguration());
        modelBuilder.ApplyConfiguration(new DictionaryEntryConfiguration());
        modelBuilder.ApplyConfiguration(new EntryVersionConfiguration());
        modelBuilder.ApplyConfiguration(new VersionTranslationConfiguration());
        modelBuilder.ApplyConfiguration(new VersionAcceptedAnswerConfiguration());
        modelBuilder.ApplyConfiguration(new LearnerWordConfiguration());
        modelBuilder.Entity<LearnerWord>().HasOne<Learner>().WithMany()
            .HasForeignKey(word => word.LearnerId)
            .HasConstraintName("learner_words_learner_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LearnerWord>().HasOne<DictionaryEntry>().WithMany()
            .HasForeignKey(word => word.EntryId)
            .HasConstraintName("learner_words_entry_id_fkey")
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.ApplyConfiguration(new LearningSessionConfiguration());
        modelBuilder.Entity<LearningSession>().HasOne<Learner>().WithMany()
            .HasForeignKey(session => session.LearnerId)
            .HasConstraintName("learning_sessions_learner_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.ApplyConfiguration(new SessionItemConfiguration());
        modelBuilder.Entity<SessionItem>().HasOne<DictionaryEntry>().WithMany()
            .HasForeignKey(item => item.EntryId)
            .HasConstraintName("session_items_entry_id_fkey")
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.ApplyConfiguration(new AnswerEventConfiguration());
        modelBuilder.Entity<AnswerEvent>().HasOne<Learner>().WithMany()
            .HasForeignKey(answer => answer.LearnerId)
            .HasConstraintName("answer_events_learner_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AnswerEvent>().HasOne<DictionaryEntry>().WithMany()
            .HasForeignKey(answer => answer.EntryId)
            .HasConstraintName("answer_events_entry_id_fkey")
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<AnswerEvent>().HasOne<EntryVersion>().WithMany()
            .HasForeignKey(answer => answer.VersionId)
            .HasConstraintName("answer_events_version_id_fkey")
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.ApplyConfiguration(new ContentReportConfiguration());
        modelBuilder.Entity<ContentReport>().HasOne<Learner>().WithMany()
            .HasForeignKey(report => report.LearnerId)
            .HasConstraintName("content_reports_learner_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ContentReport>().HasOne<AnswerEvent>().WithMany()
            .HasForeignKey(report => report.AnswerEventId)
            .HasConstraintName("content_reports_answer_event_id_fkey")
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.ApplyConfiguration(new AnswerNeutralizationConfiguration());
        modelBuilder.Entity<AnswerNeutralization>().HasOne<ContentReport>().WithMany()
            .HasForeignKey(neutralization => neutralization.ContentReportId)
            .HasConstraintName("answer_neutralizations_content_report_id_fkey")
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.ApplyConfiguration(new ValidationEvidenceConfiguration());
        modelBuilder.ApplyConfiguration(new PublicationLogConfiguration());
        modelBuilder.Entity<PublicationLog>().HasOne<Learner>().WithMany()
            .HasForeignKey(log => log.ActorLearnerId)
            .HasConstraintName("publication_log_actor_learner_id_fkey")
            .OnDelete(DeleteBehavior.NoAction);

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

