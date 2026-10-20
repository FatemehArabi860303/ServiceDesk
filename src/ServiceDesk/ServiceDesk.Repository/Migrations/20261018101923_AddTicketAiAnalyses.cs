using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Repository.Migrations;

public partial class AddTicketAiAnalyses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TicketAiAnalyses",
            columns: table => new
            {
                EventId = table.Column<Guid>(
                    type: "uniqueidentifier",
                    nullable: false),

                TicketId = table.Column<Guid>(
                    type: "uniqueidentifier",
                    nullable: false),

                ResultJson = table.Column<string>(
                    type: "nvarchar(max)",
                    nullable: false),

                CompletedAt = table.Column<DateTimeOffset>(
                    type: "datetimeoffset",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_TicketAiAnalyses",
                    analysis => analysis.EventId);

                table.ForeignKey(
                    name: "FK_TicketAiAnalyses_Tickets_TicketId",
                    column: analysis => analysis.TicketId,
                    principalTable: "Tickets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TicketAiAnalyses_TicketId",
            table: "TicketAiAnalyses",
            column: "TicketId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TicketAiAnalyses");
    }
}