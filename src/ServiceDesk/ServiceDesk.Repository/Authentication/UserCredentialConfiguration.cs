using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Authentication;

namespace ServiceDesk.Repository.Authentication;

public sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("UserCredentials");
        builder.HasKey(credential => credential.UserId);

        builder.Property(credential => credential.UserId).ValueGeneratedNever();
        builder.Property(credential => credential.PasswordHash).IsRequired();

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<UserCredential>(credential => credential.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
