using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxMessageClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_PublishedAt_CreatedAt",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<long>(
                name: "ClaimExpiresAt",
                table: "OutboxMessages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClaimToken",
                table: "OutboxMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAt_ClaimExpiresAt_CreatedAt",
                table: "OutboxMessages",
                columns: new[] { "PublishedAt", "ClaimExpiresAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_PublishedAt_ClaimExpiresAt_CreatedAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "ClaimExpiresAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "ClaimToken",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAt_CreatedAt",
                table: "OutboxMessages",
                columns: new[] { "PublishedAt", "CreatedAt" });
        }
    }
}
