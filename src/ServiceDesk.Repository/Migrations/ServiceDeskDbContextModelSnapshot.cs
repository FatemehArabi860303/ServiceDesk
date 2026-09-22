using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;

#nullable disable

namespace ServiceDesk.Repository.Migrations;

[DbContext(typeof(ServiceDeskDbContext))]
partial class ServiceDeskDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

        modelBuilder.Entity("ServiceDesk.Core.Users.User", builder =>
        {
            builder.Property<Guid>("Id")
                .ValueGeneratedNever()
                .HasColumnType("uniqueidentifier");

            builder.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("datetimeoffset");

            builder.Property<string>("Email")
                .IsRequired()
                .HasMaxLength(254)
                .HasColumnType("nvarchar(254)");

            builder.Property<string>("FirstName")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            builder.Property<bool>("IsActive")
                .HasColumnType("bit");

            builder.Property<string>("LastName")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            builder.Property<UserRole>("Role")
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion<string>()
                .HasColumnType("nvarchar(20)");

            builder.Property<DateTimeOffset>("UpdatedAt")
                .HasColumnType("datetimeoffset");

            builder.HasKey("Id");
            builder.HasIndex("Email").IsUnique().HasDatabaseName("UX_Users_Email");
            builder.ToTable("Users", (string)null);
        });

        modelBuilder.Entity("ServiceDesk.Core.Tickets.Ticket", builder =>
        {
            builder.Property<Guid>("Id")
                .ValueGeneratedNever()
                .HasColumnType("uniqueidentifier");

            builder.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("datetimeoffset");

            builder.Property<Guid>("CustomerUserId")
                .HasColumnType("uniqueidentifier");

            builder.Property<string>("Description")
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            builder.Property<TicketPriority>("Priority")
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion<string>()
                .HasColumnType("nvarchar(20)");

            builder.Property<TicketStatus>("Status")
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion<string>()
                .HasColumnType("nvarchar(20)");

            builder.Property<string>("Title")
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            builder.Property<DateTimeOffset>("UpdatedAt")
                .HasColumnType("datetimeoffset");

            builder.HasKey("Id");
            builder.HasIndex("CustomerUserId");
            builder.ToTable("Tickets", (string)null);
        });

        modelBuilder.Entity("ServiceDesk.Core.Tickets.TicketHistory", builder =>
        {
            builder.Property<Guid>("Id")
                .ValueGeneratedNever()
                .HasColumnType("uniqueidentifier");

            builder.Property<TicketHistoryAction>("Action")
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion<string>()
                .HasColumnType("nvarchar(20)");

            builder.Property<Guid>("ActorUserId")
                .HasColumnType("uniqueidentifier");

            builder.Property<DateTimeOffset>("OccurredAt")
                .HasColumnType("datetimeoffset");

            builder.Property<Guid>("TicketId")
                .HasColumnType("uniqueidentifier");

            builder.HasKey("Id");
            builder.HasIndex("ActorUserId");
            builder.HasIndex("TicketId");
            builder.ToTable("TicketHistories", (string)null);
        });

        modelBuilder.Entity("ServiceDesk.Core.Tickets.Ticket", builder =>
        {
            builder.HasOne("ServiceDesk.Core.Users.User", null)
                .WithMany()
                .HasForeignKey("CustomerUserId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        });

        modelBuilder.Entity("ServiceDesk.Core.Tickets.TicketHistory", builder =>
        {
            builder.HasOne("ServiceDesk.Core.Users.User", null)
                .WithMany()
                .HasForeignKey("ActorUserId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            builder.HasOne("ServiceDesk.Core.Tickets.Ticket", null)
                .WithMany("History")
                .HasForeignKey("TicketId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
    }
}
