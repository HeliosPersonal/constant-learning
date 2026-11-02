using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Content.Data;

internal sealed class PublicationLogConfiguration : IEntityTypeConfiguration<PublicationLog>
{
    public void Configure(EntityTypeBuilder<PublicationLog> builder)
    {
        builder.ToTable("publication_log");
        builder.HasKey(log => log.Id).HasName("publication_log_pkey");
        builder.Property(log => log.Id).ValueGeneratedNever();
        builder.Property(log => log.Action).HasMaxLength(16).IsRequired();
        builder.Property(log => log.ActorKind).HasMaxLength(24).IsRequired();
        builder.Property(log => log.CreatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(log => new { log.EntryId, log.CreatedAt })
            .HasDatabaseName("ix_publication_log_entry_created");
        builder.HasIndex(log => new { log.ActorKind, log.CreatedAt })
            .HasDatabaseName("ix_publication_log_actor_created");
        builder.HasIndex(log => log.FromVersionId).HasDatabaseName("ix_publication_log_from_version_id");
        builder.HasIndex(log => log.ToVersionId).HasDatabaseName("ix_publication_log_to_version_id");
        builder.HasIndex(log => log.ActorLearnerId).HasDatabaseName("ix_publication_log_actor_learner_id");
        builder.HasOne<DictionaryEntry>().WithMany()
            .HasForeignKey(log => log.EntryId)
            .HasConstraintName("publication_log_entry_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<EntryVersion>().WithMany()
            .HasForeignKey(log => log.FromVersionId)
            .HasConstraintName("publication_log_from_version_id_fkey")
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<EntryVersion>().WithMany()
            .HasForeignKey(log => log.ToVersionId)
            .HasConstraintName("publication_log_to_version_id_fkey")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
