using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Core.Users;

namespace ServiceDesk.Repository.Users;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Property(user => user.FirstName).HasMaxLength(CreateUserCore.FirstNameMaxLength).IsRequired();
        builder.Property(user => user.LastName).HasMaxLength(CreateUserCore.LastNameMaxLength).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(CreateUserCore.EmailMaxLength).IsRequired();
        builder.Property(user => user.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(user => user.IsActive).IsRequired();
        builder.Property(user => user.CreatedAt).IsRequired();
        builder.Property(user => user.UpdatedAt).IsRequired();

        builder.HasIndex(user => user.Email)
            .IsUnique()
            .HasDatabaseName("UX_Users_Email");
    }
}
