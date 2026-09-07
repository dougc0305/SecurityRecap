using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecurityRecap.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MailboxPollWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "active_window_end",
                table: "mailbox_ingest_configs",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "active_window_poll_minutes",
                table: "mailbox_ingest_configs",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "active_window_start",
                table: "mailbox_ingest_configs",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "schedule_time_zone",
                table: "mailbox_ingest_configs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "active_window_end",
                table: "mailbox_ingest_configs");

            migrationBuilder.DropColumn(
                name: "active_window_poll_minutes",
                table: "mailbox_ingest_configs");

            migrationBuilder.DropColumn(
                name: "active_window_start",
                table: "mailbox_ingest_configs");

            migrationBuilder.DropColumn(
                name: "schedule_time_zone",
                table: "mailbox_ingest_configs");
        }
    }
}
