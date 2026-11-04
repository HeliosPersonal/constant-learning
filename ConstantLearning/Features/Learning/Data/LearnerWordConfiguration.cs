using ConstantLearning.Features.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Learning.Data;

internal sealed class LearnerWordConfiguration : IEntityTypeConfiguration<LearnerWord>
{
    public void Configure(EntityTypeBuilder<LearnerWord> builder)
    {
        builder.ToTable("learner_words");
        builder.HasKey(word => word.Id).HasName("learner_words_pkey");
        builder.Property(word => word.Id).ValueGeneratedNever();
        builder.Property(word => word.TargetLanguage).HasMaxLength(2).IsRequired();
        builder.Property(word => word.Status).HasMaxLength(16).IsRequired();
        builder.Property(word => word.ModelState).HasColumnType("jsonb").IsRequired();
        builder.Property(word => word.SchedulerVersion).HasMaxLength(16).IsRequired();
        builder.Property(word => word.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(word => word.Version).IsRowVersion();
        builder.HasIndex(word => new { word.LearnerId, word.EntryId }).IsUnique()
            .HasDatabaseName("ux_learner_words_learner_entry");
        builder.HasIndex(word => new { word.LearnerId, word.DueOn })
            .HasDatabaseName("ix_learner_words_learner_due");
        builder.HasIndex(word => new { word.LearnerId, word.Status })
            .HasDatabaseName("ix_learner_words_learner_status");
        builder.HasIndex(word => word.EntryId).HasDatabaseName("ix_learner_words_entry_id");
        // Мово-свідомі лічильники (AC-05/AC-10); старі не-мово-свідомі лишаються additively (чистка — contract-фаза).
        builder.HasIndex(word => new { word.LearnerId, word.TargetLanguage, word.DueOn })
            .HasDatabaseName("ix_learner_words_learner_lang_due");
        builder.HasIndex(word => new { word.LearnerId, word.TargetLanguage, word.Status })
            .HasDatabaseName("ix_learner_words_learner_lang_status");
        // FK target_language покрито власним індексом (провідна колонка композитів — learner_id, не target_language).
        builder.HasIndex(word => word.TargetLanguage).HasDatabaseName("ix_learner_words_target_language");
        builder.HasOne<TargetLanguage>().WithMany()
            .HasForeignKey(word => word.TargetLanguage)
            .HasPrincipalKey(language => language.Code)
            .HasConstraintName("learner_words_target_language_fkey")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
