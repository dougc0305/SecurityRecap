using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecurityRecap.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserManagementAndServiceAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "must_change_password",
                table: "asp_net_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE asp_net_users SET role = 'Manager' WHERE role IN ('PropertyManager', 'SecurityPersonnel');");
            migrationBuilder.Sql("UPDATE asp_net_users SET role = 'Viewer' WHERE role = 'BoardMember';");

            migrationBuilder.CreateTable(
                name: "service_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_assignments__tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_service_assignments_properties_property_id",
                        column: x => x.property_id,
                        principalTable: "properties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_assignments_property_id_tenant_id_role",
                table: "service_assignments",
                columns: new[] { "property_id", "tenant_id", "role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_assignments_tenant_id",
                table: "service_assignments",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_assignments");

            migrationBuilder.DropColumn(
                name: "must_change_password",
                table: "asp_net_users");
        }
    }
}
