using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Authentication;

namespace ServiceDesk.Repository.Authentication;

public sealed class UserAccessProvisionConfiguration : IEntityTypeConfiguration<UserAccessProvision>
{
    public void Configure(EntityTypeBuilder<UserAccessProvision> builder)
    {
        builder.ToTable("UserAccessProvisions");
        builder.HasKey(provision => provision.UserId);

        builder.Property(provision => provision.UserId).ValueGeneratedNever();
        builder.Property(provision => provision.ActivationTokenHash)
            .HasColumnType("binary(32)")
            .IsRequired();
        builder.Property(provision => provision.ExpiresAt).IsRequired();

        builder.HasIndex(provision => provision.ActivationTokenHash)
            .IsUnique()
            .HasDatabaseName("UX_UserAccessProvisions_ActivationTokenHash");

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<UserAccessProvision>(provision => provision.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
