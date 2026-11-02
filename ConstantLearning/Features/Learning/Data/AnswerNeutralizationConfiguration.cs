using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstantLearning.Features.Learning.Data;

internal sealed class AnswerNeutralizationConfiguration : IEntityTypeConfiguration<AnswerNeutralization>
{
    public void Configure(EntityTypeBuilder<AnswerNeutralization> builder)
    {
        builder.ToTable("answer_neutralizations");
        builder.HasKey(neutralization => neutralization.Id).HasName("answer_neutralizations_pkey");
        builder.Property(neutralization => neutralization.Id).ValueGeneratedNever();
        builder.Property(neutralization => neutralization.CreatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(neutralization => neutralization.AnswerEventId).IsUnique()
            .HasDatabaseName("ux_answer_neutralizations_answer_event");
        builder.HasIndex(neutralization => neutralization.ContentReportId)
            .HasDatabaseName("ix_answer_neutralizations_content_report_id");
        builder.HasOne<AnswerEvent>().WithOne()
            .HasForeignKey<AnswerNeutralization>(neutralization => neutralization.AnswerEventId)
            .HasConstraintName("answer_neutralizations_answer_event_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
