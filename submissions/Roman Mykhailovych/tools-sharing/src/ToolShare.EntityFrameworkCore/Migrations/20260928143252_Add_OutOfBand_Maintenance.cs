using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolShare.Migrations
{
    /// <inheritdoc />
    public partial class Add_OutOfBand_Maintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "TriggeringLoanId",
                schema: "lending",
                table: "MaintenanceRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "ObservedCondition",
                schema: "lending",
                table: "MaintenanceRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Origin",
                schema: "lending",
                table: "MaintenanceRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReportReason",
                schema: "lending",
                table: "MaintenanceRequests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReportedByMemberId",
                schema: "lending",
                table: "MaintenanceRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaintenanceRequests_OriginShape",
                schema: "lending",
                table: "MaintenanceRequests",
                sql: "(\"Origin\" = 0 AND \"TriggeringLoanId\" IS NOT NULL AND \"ReportedByMemberId\" IS NULL AND \"ReportReason\" IS NULL AND \"ObservedCondition\" IS NULL) OR (\"Origin\" = 1 AND \"TriggeringLoanId\" IS NULL AND \"ReportedByMemberId\" IS NOT NULL AND \"ReportReason\" IS NOT NULL AND \"ObservedCondition\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MaintenanceRequests_OriginShape",
                schema: "lending",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "ObservedCondition",
                schema: "lending",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "Origin",
                schema: "lending",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "ReportReason",
                schema: "lending",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "ReportedByMemberId",
                schema: "lending",
                table: "MaintenanceRequests");

            migrationBuilder.AlterColumn<Guid>(
                name: "TriggeringLoanId",
                schema: "lending",
                table: "MaintenanceRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
