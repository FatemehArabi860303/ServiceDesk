using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;

namespace ServiceDesk.Repository.Tickets;

public sealed class TicketHistoryConfiguration : IEntityTypeConfiguration<TicketHistory>
{
    public void Configure(EntityTypeBuilder<TicketHistory> builder)
    {
        builder.ToTable("TicketHistories");
        builder.HasKey(history => history.Id);

        builder.Property(history => history.Id).ValueGeneratedNever();
        builder.Property(history => history.TicketId).IsRequired();
        builder.Property(history => history.ActorUserId).IsRequired();
        builder.Property(history => history.Action).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(history => history.OccurredAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(history => history.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
