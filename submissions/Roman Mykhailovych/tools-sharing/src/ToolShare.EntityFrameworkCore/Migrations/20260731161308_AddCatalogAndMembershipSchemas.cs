using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolShare.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogAndMembershipSchemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.EnsureSchema(
                name: "membership");

            migrationBuilder.CreateTable(
                name: "Categories",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityRules",
                schema: "membership",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxLoanTermDays = table.Column<int>(type: "integer", nullable: false),
                    ConcurrentLoanLimit = table.Column<int>(type: "integer", nullable: false),
                    LowRatingThreshold = table.Column<int>(type: "integer", nullable: false),
                    ReducedConcurrentLoanLimit = table.Column<int>(type: "integer", nullable: false),
                    OverduePenaltyPoints = table.Column<int>(type: "integer", nullable: false),
                    DamagePenaltyPoints = table.Column<int>(type: "integer", nullable: false),
                    CleanReturnRewardPoints = table.Column<int>(type: "integer", nullable: false),
                    WaitlistOfferWindowHours = table.Column<int>(type: "integer", nullable: false),
                    ReminderLeadTimeDays = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_CommunityRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                schema: "membership",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentityUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CurrentRating = table.Column<int>(type: "integer", nullable: false),
                    EnrolledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    StatusChangeReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_Members", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tools",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
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
                    table.PrimaryKey("PK_Tools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tools_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "catalog",
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemberStandingChanges",
                schema: "membership",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PreviousStatus = table.Column<int>(type: "integer", nullable: true),
                    NewStatus = table.Column<int>(type: "integer", nullable: true),
                    PreviousRole = table.Column<int>(type: "integer", nullable: true),
                    NewRole = table.Column<int>(type: "integer", nullable: true),
                    OutcomeType = table.Column<int>(type: "integer", nullable: true),
                    RawPoints = table.Column<int>(type: "integer", nullable: true),
                    EffectivePoints = table.Column<int>(type: "integer", nullable: true),
                    ResultingRating = table.Column<int>(type: "integer", nullable: true),
                    OccurrenceId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberStandingChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberStandingChanges_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "membership",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ToolInstances",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolId = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NormalizedSerialNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Condition = table.Column<int>(type: "integer", nullable: false),
                    CirculationState = table.Column<int>(type: "integer", nullable: false),
                    RetirementReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RetiredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("PK_ToolInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolInstances_Tools_ToolId",
                        column: x => x.ToolId,
                        principalSchema: "catalog",
                        principalTable: "Tools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ToolInstancePhotos",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlobName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolInstancePhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolInstancePhotos_ToolInstances_ToolInstanceId",
                        column: x => x.ToolInstanceId,
                        principalSchema: "catalog",
                        principalTable: "ToolInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ToolInstanceStateChanges",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousCondition = table.Column<int>(type: "integer", nullable: true),
                    NewCondition = table.Column<int>(type: "integer", nullable: false),
                    PreviousCirculationState = table.Column<int>(type: "integer", nullable: true),
                    NewCirculationState = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolInstanceStateChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolInstanceStateChanges_ToolInstances_ToolInstanceId",
                        column: x => x.ToolInstanceId,
                        principalSchema: "catalog",
                        principalTable: "ToolInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_NormalizedName",
                schema: "catalog",
                table: "Categories",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Members_Email",
                schema: "membership",
                table: "Members",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Members_IdentityUserId",
                schema: "membership",
                table: "Members",
                column: "IdentityUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Members_Status_Role",
                schema: "membership",
                table: "Members",
                columns: new[] { "Status", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberStandingChanges_MemberId_ChangedAt",
                schema: "membership",
                table: "MemberStandingChanges",
                columns: new[] { "MemberId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberStandingChanges_OccurrenceId_OutcomeType",
                schema: "membership",
                table: "MemberStandingChanges",
                columns: new[] { "OccurrenceId", "OutcomeType" },
                unique: true,
                filter: "\"OccurrenceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ToolInstancePhotos_ToolInstanceId",
                schema: "catalog",
                table: "ToolInstancePhotos",
                column: "ToolInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolInstances_CirculationState_Condition",
                schema: "catalog",
                table: "ToolInstances",
                columns: new[] { "CirculationState", "Condition" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolInstances_NormalizedSerialNumber",
                schema: "catalog",
                table: "ToolInstances",
                column: "NormalizedSerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToolInstances_ToolId",
                schema: "catalog",
                table: "ToolInstances",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolInstanceStateChanges_ToolInstanceId_ChangedAt",
                schema: "catalog",
                table: "ToolInstanceStateChanges",
                columns: new[] { "ToolInstanceId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Tools_CategoryId",
                schema: "catalog",
                table: "Tools",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Tools_NormalizedName",
                schema: "catalog",
                table: "Tools",
                column: "NormalizedName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityRules",
                schema: "membership");

            migrationBuilder.DropTable(
                name: "MemberStandingChanges",
                schema: "membership");

            migrationBuilder.DropTable(
                name: "ToolInstancePhotos",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ToolInstanceStateChanges",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Members",
                schema: "membership");

            migrationBuilder.DropTable(
                name: "ToolInstances",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Tools",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Categories",
                schema: "catalog");
        }
    }
}
