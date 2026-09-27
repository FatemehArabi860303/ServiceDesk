using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ServiceDesk.Repository.Notifications;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.Type).HasMaxLength(100).IsRequired();
        builder.Property(message => message.Payload).IsRequired();
        builder.Property(message => message.OccurredAt).IsRequired();
        builder.Property(message => message.CreatedAt).IsRequired();
        builder.Property(message => message.PublishedAt);
        builder.Property(message => message.ClaimToken);
        builder.Property(message => message.ClaimExpiresAt)
            .HasConversion(new DateTimeOffsetToBinaryConverter());
        builder.HasIndex(message => new { message.PublishedAt, message.ClaimExpiresAt, message.CreatedAt });
    }
}
