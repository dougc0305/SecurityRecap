using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecurityRecap.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MailboxFailureAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "receives_alerts",
                table: "user_properties",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_alert_at",
                table: "mailbox_ingest_configs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_alert_signature",
                table: "mailbox_ingest_configs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_report_ingested_at",
                table: "mailbox_ingest_configs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "stale_after_hours",
                table: "mailbox_ingest_configs",
                type: "integer",
                nullable: false,
                defaultValue: 26);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "receives_alerts",
                table: "user_properties");

            migrationBuilder.DropColumn(
                name: "last_alert_at",
                table: "mailbox_ingest_configs");

            migrationBuilder.DropColumn(
                name: "last_alert_signature",
                table: "mailbox_ingest_configs");

            migrationBuilder.DropColumn(
                name: "last_report_ingested_at",
                table: "mailbox_ingest_configs");

            migrationBuilder.DropColumn(
                name: "stale_after_hours",
                table: "mailbox_ingest_configs");
        }
    }
}
