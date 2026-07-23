# Task 1 Report: connectionRange/connectionHeightRange スキーマ追加

## Codex delegation

- Session UUID: `019f8d39-a3a9-7fa0-ae64-7d44a80d4986`
- Delegated: blocks.yml の8機械when句への `connectionRange`/`connectionHeightRange` プロパティ追加、forUnitTest blocks.json 16エントリ更新、EditModeInPlayingTestMod blocks.json 3エントリ更新。maxWireLength削除禁止、対象3ファイル以外変更禁止、コミット禁止を明記。
- Codex self-report: 3ファイルとも完了、maxWireLength保持、ElectricPole未変更、JSON構文チェック済み、対象ファイル以外の変更なし。

## Own verification

1. **git diff review (full)**: 全3ファイルのdiffを目視確認。
   - `VanillaSchema/blocks.yml`: 8箇所（ElectricMachine, ElectricGenerator, ElectricMiner, ElectricPump, GearToElectricGenerator, ElectricToGearGenerator, CleanRoomAirFilter, CleanRoomMachine）に `connectionRange: type integer default 30` / `connectionHeightRange: type integer default 20` を追加。`ElectricPole` のwhen句は無変更を確認。
   - `forUnitTest/blocks.json`: `connectionRange: 9`/`connectionHeightRange: 9` の追加が過不足なく16件（grep -c で確認）。`maxWireLength` エントリは18件のまま維持（16+ElectricPole2件）。TestElectricPole/TestLockedElectricPoleは未変更（python検証で `connectionRange` 不在を確認）。
   - `EditModeInPlayingTestMod/blocks.json`: `connectionRange: 30`/`connectionHeightRange: 20` を3エントリ（キャンプファイア/釜/TestElectricToGearGeneratorUI）に追加。
2. **JSON構文検証**: `python3 -c "json.load(...)"` で両JSONファイルとも正常パース確認。`git diff --check` も0件。
3. **SourceGenerator再生成**: `_CompileRequester.cs` の `dummyText` を更新してトリガー。
4. **コンパイル**: `uloop compile --project-path ./moorestech_client --force-recompile true --wait-for-domain-reload true` → `Success: true, ErrorCount: 0, WarningCount: 0`。
   - 途中 `UnityMcpSettings.json not found` エラーが発生（既知の.bak問題）。`UnityMcpSettings.json.bak` を `UnityMcpSettings.json` にコピーして復旧。
5. **生成コード確認**: `uloop execute-dynamic-code` で `Mooresmaster.Model.BlocksModule.ElectricMachineBlockParam` の全プロパティ名をリフレクション取得し、`ConnectionRange` / `ConnectionHeightRange` が `MaxWireLength` と共存していることを実行時に確認した。
   出力: `RequiredPower,IdlePowerRate,InputSlotCount,OutputSlotCount,InventoryConnectors,InputTankCount,OutputTankCount,InnerTankCapacity,ModuleSlotCount,FluidInventoryConnectors,MaxWireConnectionCount,MaxWireLength,ConnectionRange,ConnectionHeightRange`
   （ファイルシステム上に生成.csファイルが存在しない＝インメモリRoslyn SourceGeneratorのため、静的grepでの確認は不可能だった。実行時リフレクションで代替検証。）

## Files changed / commit

- `VanillaSchema/blocks.yml`
- `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`
- `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json`
- `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`（SourceGenerator再生成トリガー、dummyText更新）

Commit: `5a4e46587` "feat: 機械系ブロックにconnectionRange/connectionHeightRangeスキーマを追加"

## Issues / concerns

- コミット前後で `.moorestech-external-revisions.json` が意図せず変更された（`moorestech_master` の外部リビジョンピンがUnity/uloop実行中に自動更新されたと思われる、commitHashが `b5d4454bc...` → `c80cee8ba...` に変化）。Task 1のスコープ外のためコミットに含めず、作業ツリーに未コミットのまま残置している（他タスクとの干渉は想定していないが、要注意）。
- `UnityMcpSettings.json` が実行中に消失/欠損した（既知の環境問題、過去メモリの `uloop-mcp-settings-bak` と同一事象）。`.bak` から復元して対処済み。後続タスクでも同様の事象が起きうる。
