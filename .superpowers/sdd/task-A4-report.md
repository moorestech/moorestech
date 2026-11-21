# Task A4 実装報告

## 実装

- 拠点を追跡するときに A3 の `IOrbitalCargoSelectorComponent` を保持し、接続要求では `AlreadyConnected` 判定の後、ケーブル消費の前に未選択を拒否する `CargoNotSelected` を追加した。拒否理由は既存の `Refuse` 経由でログに出る。
- 未選択時の拒否・ケーブル残量維持・選択後の接続成功・接続済み判定の優先順を検証する統合テストを追加した。
- 接続を前提としていたケーブル返却・容量境界テストの拠点に積荷選択を追加した。

## 実行と証拠

- RED: `uloop compile --project-path ./moorestech_client`。追加したテストが `CargoNotSelected` 未定義（CS0117）で失敗。観測箇所: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/OrbitalCargo/OrbitalCargoBaseConnectTest.cs:34`。
- Compile: 同コマンド成功、ErrorCount 0、WarningCount 181。
- `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Tests.CombinedTest.Game.OrbitalCargo.OrbitalCargoBaseConnectTest"`: 2/2 成功。
- `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.CombinedTest\.(Game\.(SpaceElevator|Tier9|RocketLaunchPad\.Regression)|Server\.PacketTest\.(SpaceElevator))\."`: 117/117 成功。
- `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.CombinedTest\.Server\.PacketTest\.(Belt(Connection)?RemovalRefund|SpaceElevatorRemovalCapacityTest)"`: 31/31 成功。
- 証拠の実行対象: 上記テストソースと [A4実装報告](task-A4-report.md)。成功時は `uloop run-tests` が NUnit XML を保存しない仕様のため、XMLパスは返らず (`XmlPath: null`)、CLI出力の成功件数を記録した。

## 自己確認

- `git diff --check` 成功。変更したC#は全て200行未満、`partial`・`Func<>` の追加なし。
- `_CompileRequester.cs` に差分なし。Unityが新規テストに生成した `.meta` はコミット対象に含める。
- 既存テストの網羅はブリーフ指定の3組で実行し、すべて成功。
- 懸念: コンパイルには181警告があった（エラーなし）。今回の差分に関する失敗は観測していない。

## 変更ファイル

- `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/SpaceElevator/SpaceElevatorConnectResult.cs`
- `moorestech_server/Assets/Scripts/Game.SpaceElevator/SpaceElevatorConnectionService.cs`
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/OrbitalCargo/OrbitalCargoBaseConnectTest.cs` と Unity生成 `.meta`
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorCablePoolTestBoundaries.cs`
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BeltRemoval/SpaceElevatorRemovalCapacityTest.cs`
- `.superpowers/sdd/task-A4-report.md`

Base: `4f08e34951`
Head: `1c45199d29`（A4実装コミット。報告は後続の記録コミットに格納）。
