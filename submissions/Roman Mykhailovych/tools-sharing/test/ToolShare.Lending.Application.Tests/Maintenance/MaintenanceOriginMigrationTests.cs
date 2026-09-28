using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Shouldly;
using ToolShare.Lending.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// 008 migration shape (FR-015, FR-016, MAINT-01, MAINT-04): pre-feature rows
/// read as return-triggered, the <c>CK_MaintenanceRequests_OriginShape</c>
/// constraint rejects a row mixing both origins, and the one-open-request
/// index spans both origins. Runs against the migrated template database.
/// </summary>
public class MaintenanceOriginMigrationTests : LendingAuthorizationTestBase
{
    private readonly IMaintenanceRequestRepository _maintenanceRequestRepository;
    private readonly IDbContextProvider<LendingDbContext> _dbContextProvider;

    public MaintenanceOriginMigrationTests()
    {
        _maintenanceRequestRepository = GetRequiredService<IMaintenanceRequestRepository>();
        _dbContextProvider = GetRequiredService<IDbContextProvider<LendingDbContext>>();
    }

    [Fact]
    public async Task A_request_created_the_pre_feature_way_round_trips_as_return_triggered_with_its_loan()
    {
        var loanId = Guid.NewGuid();
        var id = Guid.NewGuid();

        await WithUnitOfWorkAsync(() =>
            _maintenanceRequestRepository.InsertAsync(new MaintenanceRequest(id, Guid.NewGuid(), loanId, DateTime.UtcNow)));

        var reloaded = await WithUnitOfWorkAsync(() => _maintenanceRequestRepository.GetAsync(id));

        reloaded.Origin.ShouldBe(MaintenanceRequestOrigin.ReturnTriggered);
        reloaded.TriggeringLoanId.ShouldBe(loanId);
        reloaded.ReportedByMemberId.ShouldBeNull();
        reloaded.ReportReason.ShouldBeNull();
        reloaded.ObservedCondition.ShouldBeNull();
    }

    [Fact]
    public async Task A_row_with_no_origin_column_value_defaults_to_return_triggered()
    {
        var id = Guid.NewGuid();

        await ExecuteRawInsertAsync(id, originColumn: false, origin: 0, triggeringLoanId: Guid.NewGuid(), reportReason: null);

        var reloaded = await WithUnitOfWorkAsync(() => _maintenanceRequestRepository.GetAsync(id));
        reloaded.Origin.ShouldBe(MaintenanceRequestOrigin.ReturnTriggered);
    }

    [Fact]
    public async Task An_out_of_band_row_that_also_carries_a_loan_is_rejected_by_the_origin_shape_constraint()
    {
        var exception = await Should.ThrowAsync<PostgresException>(() =>
            ExecuteRawInsertAsync(Guid.NewGuid(), originColumn: true, origin: 1, triggeringLoanId: Guid.NewGuid(), reportReason: "Cracked"));

        exception.ConstraintName.ShouldBe("CK_MaintenanceRequests_OriginShape");
    }

    [Fact]
    public async Task A_return_triggered_row_that_also_carries_a_report_reason_is_rejected_by_the_origin_shape_constraint()
    {
        var exception = await Should.ThrowAsync<PostgresException>(() =>
            ExecuteRawInsertAsync(Guid.NewGuid(), originColumn: true, origin: 0, triggeringLoanId: Guid.NewGuid(), reportReason: "Cracked"));

        exception.ConstraintName.ShouldBe("CK_MaintenanceRequests_OriginShape");
    }

    [Fact]
    public async Task The_one_open_request_index_spans_both_origins()
    {
        var instanceId = Guid.NewGuid();

        await WithUnitOfWorkAsync(() =>
            _maintenanceRequestRepository.InsertAsync(new MaintenanceRequest(Guid.NewGuid(), instanceId, Guid.NewGuid(), DateTime.UtcNow)));

        await Should.ThrowAsync<DbUpdateException>(() => WithUnitOfWorkAsync(() =>
            _maintenanceRequestRepository.InsertAsync(
                MaintenanceRequest.ReportOutOfBand(Guid.NewGuid(), instanceId, Guid.NewGuid(), "Loose guard", Catalog.ToolCondition.Good, DateTime.UtcNow),
                autoSave: true)));
    }

    [Fact]
    public async Task A_pre_feature_request_appears_in_the_cost_report_as_return_triggered_with_its_loan_and_cost()
    {
        var loanId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var closedAt = DateTime.UtcNow.AddDays(-1);
        var request = new MaintenanceRequest(Guid.NewGuid(), instanceId, loanId, closedAt.AddDays(-3));
        request.Close(closedAt, 42.50m);

        await WithUnitOfWorkAsync(() => _maintenanceRequestRepository.InsertAsync(request));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Reports.MaintenanceCostReportDto report;
        using (AsLibrarian())
        {
            report = await GetRequiredService<Reports.IReportAppService>().GetMaintenanceCostAsync(
                new Reports.MaintenanceCostReportInput { From = today.AddDays(-7), To = today });
        }

        var item = report.Items.Single(i => i.MaintenanceRequestId == request.Id);
        item.Origin.ShouldBe(MaintenanceRequestOrigin.ReturnTriggered);
        item.TriggeringLoanId.ShouldBe(loanId);
        item.Cost.ShouldBe(42.50m);
        report.ReturnTriggeredSubtotal.ShouldBe(42.50m);
    }

    private Task ExecuteRawInsertAsync(Guid id, bool originColumn, int origin, Guid? triggeringLoanId, string? reportReason)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await _dbContextProvider.GetDbContextAsync();
            var originName = originColumn ? "\"Origin\", " : string.Empty;
            var originValue = originColumn ? "@origin, " : string.Empty;

            // Status = 1 (Closed), so these rows never collide with the
            // one-open-request index — only the origin shape is under test.
            await dbContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO lending.\"MaintenanceRequests\" " +
                "(\"Id\", \"ToolInstanceId\", \"TriggeringLoanId\", " + originName + "\"ReportReason\", \"Status\", \"OpenedAt\", \"ExtraProperties\", \"ConcurrencyStamp\", \"CreationTime\") " +
                "VALUES (@id, @instanceId, @loanId, " + originValue + "@reason, 1, now(), '{{}}', 'stamp', now())",
                new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = id },
                new NpgsqlParameter("instanceId", NpgsqlDbType.Uuid) { Value = Guid.NewGuid() },
                new NpgsqlParameter("loanId", NpgsqlDbType.Uuid) { Value = (object?)triggeringLoanId ?? DBNull.Value },
                new NpgsqlParameter("reason", NpgsqlDbType.Varchar) { Value = (object?)reportReason ?? DBNull.Value },
                new NpgsqlParameter("origin", NpgsqlDbType.Integer) { Value = origin });
        });
    }
}
