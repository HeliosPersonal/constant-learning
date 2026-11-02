using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Learning.Data;

internal sealed class SessionItemConfiguration : IEntityTypeConfiguration<SessionItem>
{
    public void Configure(EntityTypeBuilder<SessionItem> builder)
    {
        builder.ToTable("session_items");
        builder.HasKey(item => item.Id).HasName("session_items_pkey");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Kind).HasMaxLength(24).IsRequired();
        builder.Property(item => item.State).HasMaxLength(16).IsRequired();
        builder.Property(item => item.AnswerPayloadHash).HasMaxLength(64);
        builder.Property(item => item.ResponsePayload).HasColumnType("jsonb");
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(item => item.Version).IsRowVersion();
        builder.HasIndex(item => new { item.SessionId, item.Position }).IsUnique()
            .HasDatabaseName("ux_session_items_session_position");
        builder.HasIndex(item => item.EntryId).HasDatabaseName("ix_session_items_entry_id");
        builder.HasOne(item => item.Session).WithMany()
            .HasForeignKey(item => item.SessionId)
            .HasConstraintName("session_items_session_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
