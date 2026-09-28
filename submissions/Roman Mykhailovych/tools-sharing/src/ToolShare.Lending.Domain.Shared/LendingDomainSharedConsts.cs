namespace ToolShare.Lending;

/// <summary>Field-length limits shared between validation and EF Core mapping.</summary>
public static class LendingDomainSharedConsts
{
    public const int CancellationReasonMaxLength = 512;

    /// <summary>008 MAINT-05 (FR-002): the reason a Librarian gives for an out-of-band maintenance report.</summary>
    public const int MaintenanceReportReasonMaxLength = 500;
}
