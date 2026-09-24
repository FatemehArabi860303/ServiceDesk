using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;

namespace ServiceDesk.Repository.Tickets;

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");
        builder.HasKey(ticket => ticket.Id);

        builder.Property(ticket => ticket.Id).ValueGeneratedNever();
        builder.Property(ticket => ticket.CustomerUserId).IsRequired();
        builder.Property(ticket => ticket.AssignedEmployeeUserId);
        builder.Property(ticket => ticket.Title).IsRequired();
        builder.Property(ticket => ticket.Description).IsRequired();
        builder.Property(ticket => ticket.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(ticket => ticket.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(ticket => ticket.CreatedAt).IsRequired();
        builder.Property(ticket => ticket.UpdatedAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(ticket => ticket.CustomerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(ticket => ticket.AssignedEmployeeUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ticket => ticket.AssignedEmployeeUserId);

        builder.HasMany(ticket => ticket.History)
            .WithOne()
            .HasForeignKey(history => history.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(ticket => ticket.History)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
