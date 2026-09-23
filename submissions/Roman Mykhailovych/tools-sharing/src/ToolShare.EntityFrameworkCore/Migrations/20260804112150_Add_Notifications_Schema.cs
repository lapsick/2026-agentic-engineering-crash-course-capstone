using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolShare.Migrations
{
    /// <inheritdoc />
    public partial class Add_Notifications_Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    OriginatingLoanId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginatingToolInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginatingChangedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DisplayText = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveryRecords",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FailureDetail = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveryRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveryRecords_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalSchema: "notifications",
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryRecords_NotificationId",
                schema: "notifications",
                table: "NotificationDeliveryRecords",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_MemberId_CreatedAt",
                schema: "notifications",
                table: "Notifications",
                columns: new[] { "MemberId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_MemberId_Kind_OriginatingChangedAt",
                schema: "notifications",
                table: "Notifications",
                columns: new[] { "MemberId", "Kind", "OriginatingChangedAt" },
                unique: true,
                filter: "\"OriginatingChangedAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_MemberId_Kind_OriginatingLoanId",
                schema: "notifications",
                table: "Notifications",
                columns: new[] { "MemberId", "Kind", "OriginatingLoanId" },
                unique: true,
                filter: "\"OriginatingLoanId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDeliveryRecords",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "notifications");
        }
    }
}
