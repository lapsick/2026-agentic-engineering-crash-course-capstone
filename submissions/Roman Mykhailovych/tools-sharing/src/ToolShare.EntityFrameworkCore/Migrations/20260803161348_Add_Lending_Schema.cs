using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolShare.Migrations
{
    /// <inheritdoc />
    public partial class Add_Lending_Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "lending");

            // Required by the exclusion constraint below (research R3): lets
            // PostgreSQL's EXCLUDE constraint use a plain equality term
            // ("ToolInstanceId") alongside the range-overlap (&&) operator in
            // the same GiST index. EF Core's fluent API has no first-class
            // builder for this, hence the raw SQL.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.CreateTable(
                name: "Loans",
                schema: "lending",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedOutAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PlannedReturnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ConditionAtCheckout = table.Column<int>(type: "integer", nullable: false),
                    ReturnedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReturnedCondition = table.Column<int>(type: "integer", nullable: true),
                    IsOverdue = table.Column<bool>(type: "boolean", nullable: false),
                    ReminderSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    OverdueNoticeSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReliabilityReportedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_Loans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceRequests",
                schema: "lending",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggeringLoanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric", nullable: true),
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
                    table.PrimaryKey("PK_MaintenanceRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                schema: "lending",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaitlistEntries",
                schema: "lending",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    OfferState = table.Column<int>(type: "integer", nullable: false),
                    OfferedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    OfferExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RealizedReservationId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_WaitlistEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Loans_MemberId",
                schema: "lending",
                table: "Loans",
                column: "MemberId",
                filter: "\"ReturnedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Loans_ToolInstanceId",
                schema: "lending",
                table: "Loans",
                column: "ToolInstanceId",
                filter: "\"ReturnedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_ToolInstanceId",
                schema: "lending",
                table: "MaintenanceRequests",
                column: "ToolInstanceId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_MemberId",
                schema: "lending",
                table: "Reservations",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ToolInstanceId_Status",
                schema: "lending",
                table: "Reservations",
                columns: new[] { "ToolInstanceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_MemberId_ToolInstanceId",
                schema: "lending",
                table: "WaitlistEntries",
                columns: new[] { "MemberId", "ToolInstanceId" },
                unique: true,
                filter: "\"OfferState\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_ToolInstanceId_JoinedAt",
                schema: "lending",
                table: "WaitlistEntries",
                columns: new[] { "ToolInstanceId", "JoinedAt" });

            // RES-03/FR-002/FR-009 (research R3): the authority under
            // concurrency for "no two Active reservations for the same
            // instance may have overlapping date ranges" — a range-aware
            // exclusion constraint, since a plain unique index can only
            // forbid identical values, not overlapping ranges. Scoped to
            // Status = 0 (Active) so a Cancelled/CheckedOut row never blocks
            // a new reservation for the same dates.
            migrationBuilder.Sql(
                "ALTER TABLE lending.\"Reservations\" " +
                "ADD CONSTRAINT \"EX_Reservations_NoOverlap\" " +
                "EXCLUDE USING gist (\"ToolInstanceId\" WITH =, daterange(\"StartDate\", \"EndDate\", '[]') WITH &&) " +
                "WHERE (\"Status\" = 0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Loans",
                schema: "lending");

            migrationBuilder.DropTable(
                name: "MaintenanceRequests",
                schema: "lending");

            migrationBuilder.DropTable(
                name: "Reservations",
                schema: "lending");

            migrationBuilder.DropTable(
                name: "WaitlistEntries",
                schema: "lending");
        }
    }
}
