using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Core.Tickets;

namespace ServiceDesk.Repository.AiAnalysis;

public sealed class TicketAiAnalysisConfiguration
    : IEntityTypeConfiguration<TicketAiAnalysis>
{
    public void Configure(EntityTypeBuilder<TicketAiAnalysis> builder)
    {
        builder.ToTable("TicketAiAnalyses");

        builder.HasKey(analysis => analysis.EventId);

        builder.Property(analysis => analysis.EventId)
            .ValueGeneratedNever();

        builder.Property(analysis => analysis.TicketId)
            .IsRequired();

        builder.Property(analysis => analysis.ResultJson)
            .IsRequired();

        builder.Property(analysis => analysis.CompletedAt)
            .IsRequired();

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(analysis => analysis.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(analysis => analysis.TicketId);
    }
}