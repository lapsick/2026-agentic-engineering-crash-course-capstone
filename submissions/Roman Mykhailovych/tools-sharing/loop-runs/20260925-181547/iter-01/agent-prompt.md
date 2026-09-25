You are one iteration (1 of max 5) of an automated fix loop in the ToolShare repository.
The loop ran `dotnet test test/ToolShare.Lending.Domain.Tests/ToolShare.Lending.Domain.Tests.csproj` and it is red.

Failing tests (4 of 41):
- ToolShare.Lending.Loans.LoanOverdueCalculationTests.A_loan_is_not_overdue_on_its_planned_return_date
  Message: Shouldly.ShouldAssertException : loan.IsOverdueAsOf(PlannedReturn)
    should be
False
    but was
True
  Stack:
    at ToolShare.Lending.Loans.LoanOverdueCalculationTests.A_loan_is_not_overdue_on_its_planned_return_date() in D:\Github\2026-agentic-engineering-crash-course-capstone\submissions\Roman Mykhailovych\tools-sharing\test\ToolShare.Lending.Domain.Tests\Loans\LoanOverdueCalculationTests.cs:line 42
       at System.Reflection.MethodBaseInvoker.InterpretedInvoke_Method(Object obj, IntPtr* args)
       at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
- ToolShare.Lending.Loans.LoanLifecycleTests.IsWorsened_compares_every_pair_on_the_four_level_scale(atCheckout: New, atReturn: New, expectedWorsened: False)
  Message: Shouldly.ShouldAssertException : loan.IsWorsened()
    should be
False
    but was
True
  Stack:
    at ToolShare.Lending.Loans.LoanLifecycleTests.IsWorsened_compares_every_pair_on_the_four_level_scale(ToolCondition atCheckout, ToolCondition atReturn, Boolean expectedWorsened) in D:\Github\2026-agentic-engineering-crash-course-capstone\submissions\Roman Mykhailovych\tools-sharing\test\ToolShare.Lending.Domain.Tests\Loans\LoanLifecycleTests.cs:line 98
       at InvokeStub_LoanLifecycleTests.IsWorsened_compares_every_pair_on_the_four_level_scale(Object, Span`1)
       at System.Reflection.MethodBaseInvoker.InvokeWithFewArgs(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
- ToolShare.Lending.Maintenance.MaintenanceRequestLifecycleTests.Close_with_exactly_zero_cost_is_valid
  Message: Volo.Abp.BusinessException : Exception of type 'Volo.Abp.BusinessException' was thrown.
  Stack:
    at ToolShare.Lending.Maintenance.MaintenanceRequest.Close(DateTime closedAt, Nullable`1 cost) in D:\Github\2026-agentic-engineering-crash-course-capstone\submissions\Roman Mykhailovych\tools-sharing\src\ToolShare.Lending.Domain\Maintenance\MaintenanceRequest.cs:line 55
       at ToolShare.Lending.Maintenance.MaintenanceRequestLifecycleTests.Close_with_exactly_zero_cost_is_valid() in D:\Github\2026-agentic-engineering-crash-course-capstone\submissions\Roman Mykhailovych\tools-sharing\test\ToolShare.Lending.Domain.Tests\Maintenance\MaintenanceRequestLifecycleTests.cs:line 30
       at System.Reflection.MethodBaseInvoker.InterpretedInvoke_Method(Object obj, IntPtr* args)
       at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
- ToolShare.Lending.Loans.LoanOverdueCalculationTests.The_stored_IsOverdue_flag_does_not_make_an_open_loan_report_as_overdue_before_its_date
  Message: Shouldly.ShouldAssertException : loan.IsOverdueAsOf(PlannedReturn)
    should be
False
    but was
True
  Stack:
    at ToolShare.Lending.Loans.LoanOverdueCalculationTests.The_stored_IsOverdue_flag_does_not_make_an_open_loan_report_as_overdue_before_its_date() in D:\Github\2026-agentic-engineering-crash-course-capstone\submissions\Roman Mykhailovych\tools-sharing\test\ToolShare.Lending.Domain.Tests\Loans\LoanOverdueCalculationTests.cs:line 112
       at System.Reflection.MethodBaseInvoker.InterpretedInvoke_Method(Object obj, IntPtr* args)
       at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)

Your task for THIS iteration: make exactly ONE of these pass — the first one listed (or fix the compile errors) — by changing production code under src/.

Rules:
- NEVER modify, skip or delete anything under test/. The tests are the specification (constitution Principle V, .claude/rules/testing.md). The loop aborts if test/ changes.
- Read the failing test first to understand the expected behavior, then the production code it exercises. The XML doc comments on the production code describe the intended rule.
- Keep the change minimal: fix the defect, don't refactor or touch unrelated code, don't fix other failing tests in advance — the next iteration handles them.
- You may run `dotnet build` to check compilation. Do not run the tests — the loop does that after you finish.
- End your reply with exactly one line: `FIXED: <file> — <what was wrong and what you changed>` or `BLOCKED: <reason>`.
