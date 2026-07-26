# Task 4 報告: クライアントコレクタのアダプタ化

（注: このファイルには2026-07-23の旧タスク体系の内容が残っていたため、現行ブリーフの内容で上書きした）

**経緯**: 実装subagent（impl-task4）がファイル編集後に無応答となり、コンパイル・テスト・コミットが未実行のまま停止した（Task 3と同じ停止パターン）。編集内容はブリーフ記載のコードと完全一致していたため、オーケストレーターが検証とコミットを代行した。

## 何を実装したか

`ClientElectricWireAutoConnectCollector.cs` を全面書き換えし、選定ロジックを `ElectricWireAutoConnectSelector`（Task 2の純粋コア）へ委譲する薄いアダプタにした。これによりサーバー/クライアント間の選定ロジック二重実装が完全に解消された。

- `BuildReceivedCandidates()`: `blockDataStore.BlockGameObjectByInstanceIdDictionary` を列挙し、`ElectricWireStateChangeProcessor.CurrentPartnerIds.Count`（未所持なら0）を接続数として `ElectricWireConnectCandidate` を組み立て、あわせて InstanceId → 座標 の逆引き辞書を返す
- `Collect()` は電柱設置か機械設置かで `SelectPoleTargets` / `SelectMachineTargets` を呼び分け、結果の InstanceId を座標へ復元して返す
- 旧実装が持っていたクライアント独自ロジックはすべて削除:
  - 非電気系ブロックの事前フィルタ（`TryGetWireRangeParam` による除外）→ コアのresolver判定に一本化
  - 容量判定 `capacity <= GetPartnerCount(block)` → コア内 `capacity <= CurrentConnectionCount`
  - 電柱/機械の振り分けと選定（`CollectPoleTargets` / `CollectMachineTargets` のクライアント版）→ コアへ
  - 距離順→InstanceId順ソート → コアへ
  - `EnumerateConnectableCandidates` / `GetPartnerCount` ヘルパ → 削除

## 公開APIシグネチャ（不変）

呼び出し側 `ElectricWireAutoConnectPreview.cs` は**無変更**:
- `Collect(BlockId blockId, Vector3Int position, BlockDirection direction, BlockGameObjectDataStore blockDataStore)` → `List<(Vector3Int TargetPos, float Distance)>`

## 検証

コンパイル:
```
uloop compile --project-path ./moorestech_client
→ Success: true, ErrorCount: 0, WarningCount: 0
```

テスト:
```
uloop run-tests --project-path ./moorestech_client --filter-type regex \
  --filter-value "ElectricWire|ElectricConnectionRange|WireContract"
→ Test execution completed with status: Passed
→ TestCount: 93, PassedCount: 93, FailedCount: 0, SkippedCount: 0
```

テスト側の変更は一切していない。

## 変更したファイル

- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/ClientElectricWireAutoConnectCollector.cs`（+21 / -72）

コミット: `0d4851e4c` クライアント自動接続コレクタを選定コア委譲のアダプタへ書き換え

## 自ブロック除外（コアの契約）

クライアント側は設置プレビュー（まだ設置されていないゴースト）の候補を計算するため、`BlockGameObjectDataStore` に自ブロックは存在しない。本アダプタは受信済みブロックを列挙するだけで、この性質を壊す改変はしていない。

## サーバー側との対称性

Task 3のサーバーアダプタと構造が対称になっている:

| | サーバー（Task 3） | クライアント（Task 4） |
|---|---|---|
| 列挙元 | `ServerContext.WorldBlockDatastore.BlockMasterDictionary` | `blockDataStore.BlockGameObjectByInstanceIdDictionary` |
| 接続数の源 | `connector.WireConnections.Count` | `processor.CurrentPartnerIds.Count`（未所持なら0） |
| 逆引き表 | InstanceId → `IElectricWireConnector` | InstanceId → `Vector3Int`（座標） |
| 選定 | コアに委譲 | コアに委譲 |

## 懸念事項

- 実装subagentの停止によりTDDサイクルの実演はない。本タスクはブリーフ上もTDD指定ではなく、既存テスト93件の全PASSで後方等価を担保する設計。
- クライアント側の選定結果を直接検証する自動テストは存在しない（プレビュー表示のためUI経路）。ロジック自体はサーバーと同一ソースを共有するようになったため、コア単体テスト9件が両側を同時に守る形になっている。
