using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketSelfAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedEmployeeUserId",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedEmployeeUserId",
                table: "TicketHistories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssignedEmployeeUserId",
                table: "Tickets",
                column: "AssignedEmployeeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketHistories_AssignedEmployeeUserId",
                table: "TicketHistories",
                column: "AssignedEmployeeUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketHistories_Users_AssignedEmployeeUserId",
                table: "TicketHistories",
                column: "AssignedEmployeeUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_AssignedEmployeeUserId",
                table: "Tickets",
                column: "AssignedEmployeeUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketHistories_Users_AssignedEmployeeUserId",
                table: "TicketHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Users_AssignedEmployeeUserId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_AssignedEmployeeUserId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_TicketHistories_AssignedEmployeeUserId",
                table: "TicketHistories");

            migrationBuilder.DropColumn(
                name: "AssignedEmployeeUserId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AssignedEmployeeUserId",
                table: "TicketHistories");
        }
    }
}
