using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDesk.Repository.Migrations;

[DbContext(typeof(ServiceDeskDbContext))]
[Migration("20261005120000_AddTicketClosedAt")]
public partial class AddTicketClosedAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ClosedAt",
            table: "Tickets",
            type: "datetimeoffset",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ClosedAt", table: "Tickets");
    }
}
