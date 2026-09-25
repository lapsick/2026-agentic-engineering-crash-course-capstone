You are one iteration (1 of max 3) of an automated fix loop in the ToolShare repository.
The loop ran `dotnet test test/ToolShare.Lending.Domain.Tests/ToolShare.Lending.Domain.Tests.csproj` and the module-boundary audit, and they are not both green.

Module boundary violations reported by `pwsh scripts/check-module-boundaries.ps1` (Constitution II — a module must not reference another module's Domain or EntityFrameworkCore project; cross-module coupling is allowed only via the other module's *.Application.Contracts or ILocalEventBus integration events):
- ToolShare.Lending.Application -> ToolShare.Catalog.Domain  (src\ToolShare.Lending.Application\ToolShare.Lending.Application.csproj)

Your task for THIS iteration: fix exactly ONE of these — the first one listed (or the compile errors) — by changing production code under src/. Fix a boundary violation by removing the forbidden ProjectReference and switching the code to the other module's Application.Contracts or an integration event.

Rules:
- NEVER modify, skip or delete anything under test/. The tests are the specification (constitution Principle V, .claude/rules/testing.md). The loop aborts if test/ changes.
- NEVER modify scripts/check-module-boundaries.ps1, and never add a ProjectReference from one module to another module's Domain or EntityFrameworkCore project to make something compile or pass. The loop aborts if the audit script changes.
- Read the failing test first to understand the expected behavior, then the production code it exercises. The XML doc comments on the production code describe the intended rule.
- Keep the change minimal: fix the defect, don't refactor or touch unrelated code, don't fix other failing tests in advance — the next iteration handles them.
- You may run `dotnet build` to check compilation. Do not run the tests — the loop does that after you finish.
- End your reply with exactly one line: `FIXED: <file> — <what was wrong and what you changed>` or `BLOCKED: <reason>`.
