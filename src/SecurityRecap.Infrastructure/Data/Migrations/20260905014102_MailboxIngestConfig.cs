using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecurityRecap.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MailboxIngestConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mailbox_ingest_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    graph_tenant_id = table.Column<string>(type: "text", nullable: false),
                    graph_client_id = table.Column<string>(type: "text", nullable: false),
                    graph_client_secret_protected = table.Column<string>(type: "text", nullable: false),
                    mailbox_address = table.Column<string>(type: "text", nullable: false),
                    folder_name = table.Column<string>(type: "text", nullable: false),
                    from_address = table.Column<string>(type: "text", nullable: true),
                    subject_contains = table.Column<string>(type: "text", nullable: true),
                    attachment_name_contains = table.Column<string>(type: "text", nullable: true),
                    lookback_days = table.Column<int>(type: "integer", nullable: false),
                    poll_interval_minutes = table.Column<int>(type: "integer", nullable: false),
                    mark_as_read = table.Column<bool>(type: "boolean", nullable: false),
                    move_to_folder = table.Column<string>(type: "text", nullable: true),
                    send_summary_email = table.Column<bool>(type: "boolean", nullable: false),
                    summary_recipients = table.Column<string[]>(type: "text[]", nullable: false),
                    summary_subject_prefix = table.Column<string>(type: "text", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_polled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_success_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_message_received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mailbox_ingest_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_mailbox_ingest_configs__properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mailbox_ingest_configs_property_id",
                table: "mailbox_ingest_configs",
                column: "property_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mailbox_ingest_configs");
        }
    }
}
