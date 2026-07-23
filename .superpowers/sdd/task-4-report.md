# Task 4 Report: サーバー自動接続の収集を全コネクタ列挙+相互判定へ転換

## Execution mode
Implemented directly (no codex delegation) — the brief already contained the exact, complete code for
`ElectricWireAutoConnectTargetCollector.cs`, and the required edits to the other two files were small,
mechanical, and needed close cross-referencing with existing extension-method signatures. No codex UUID.

## Files changed
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireAutoConnectTargetCollector.cs` — full rewrite per brief Step 1
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireAutoConnectService.cs` — `EvaluateAutoConnect` now builds a `BlockPositionInfo` and calls the new collector signatures; local `CollectMachineTargets` switched from `TryGetWireParam` to `TryGetWireRangeParam`
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/ElectricWireExtendService.cs` — `ExecuteExtendWithOrigin` builds a `poleGhostInfo` (`BlockPositionInfo`) and passes it to `CollectPoleMachineTargets`

## Extension-method verification
Read `ElectricWireSystemUtil.TryGetWireConnector` (`.../ElectricWire/Connection/ElectricWireSystemUtil.cs`): it resolves via `block.GetComponent<IElectricWireConnector>()` (returns null if absent), not a `TryGetComponent` pattern. I additionally read `Game.Block.Interface.Extension.BlockExtension`, which defines both `GetComponent<T>` and `TryGetComponent<T>(out T)` as extensions on `IBlock`. The brief's code already used `worldBlock.Block.TryGetComponent<IElectricWireConnector>(out var connector)`, which is a real, existing extension method — matches. One addition beyond the brief's literal snippet: the brief's `using` list omitted `Game.Block.Interface.Extension`, which is required for `TryGetComponent` to resolve; I added it to the file's usings.

## ElectricWireExtendService.cs call-site audit
Read the file in full. Only one `ElectricWireAutoConnectTargetCollector` call site needed updating: `CollectPoleMachineTargets` inside `ExecuteExtendWithOrigin` (previously ~line 100). `ExecuteIsolatedPlace` does not call the collector directly — it delegates to `ElectricWireAutoConnectService.EvaluateAutoConnect`, which was already updated in Step 2. The old direct-distance gate (`Mathf.Min(fromConnector.MaxWireLength, poleParam.MaxWireLength) < distance`, ~lines 79-81) was left untouched, per the brief (Task 5 scope).

## Diff review findings
- No behavioral surprises versus the brief's supplied code — implemented essentially verbatim.
- Confirmed `IWorldBlockDatastore.BlockMasterDictionary` (`IReadOnlyDictionary<BlockInstanceId, WorldBlockData>`) and `WorldBlockData.Block` / `.BlockPositionInfo` exist with the exact shapes the brief assumed.
- Confirmed `BlockPositionInfo.OriginalPos`, `.MinPos`, `.MaxPos` exist (used by `ElectricConnectionRangeService.IsMutuallyConnectable`/`Covers` and by the new collector's distance calc).
- `ElectricWireBlockParamResolver.TryGetWireParam` (the old, non-range-profile resolver) is still used elsewhere (`PlaceBlockProtocol.cs:99`) — left untouched, out of scope.

## Test results
Compile: `uloop compile --project-path ./moorestech_client` → `Success: true, ErrorCount: 0` (162 pre-existing, unrelated warnings).

Tests: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWireAutoConnectPlaceTest"` → `Success: true, TestCount: 4, PassedCount: 4, FailedCount: 0`. No coordinate relocations were necessary — all 4 existing test scenarios already sit within the new mutual-range boxes.

One environment hiccup during test execution: `uloop run-tests` failed with "Unity CLI Loop is not installed in this project (UserSettings/UnityMcpSettings.json not found)" after a domain-reload wait; restored via `cp moorestech_client/UserSettings/UnityMcpSettings.json.bak moorestech_client/UserSettings/UnityMcpSettings.json`, then the run succeeded. No code implication.

## Commit
`b282135e5` — "refactor: 自動接続候補収集を全コネクタ列挙+相互範囲判定へ転換" (scoped to `moorestech_server/Assets/Scripts` only; two unrelated dirty files from other in-flight tasks, `.moorestech-external-revisions.json` and `.superpowers/sdd/task-1-report.md`, were left untouched/unstaged).

## Concerns
None blocking. Minor note: the brief's Step 1 code snippet omitted the `Game.Block.Interface.Extension` using directive needed for `TryGetComponent`; added it without altering any other logic. No other deviations from the brief.

---
（このファイルは以前の別タスク番号「Task 4」（BlockCategoryMaster新設）のレポートを上書きしている。旧内容はコミット履歴で参照可能）
