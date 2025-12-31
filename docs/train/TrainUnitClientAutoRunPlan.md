# TrainUnit Client AutoRun Plan

現在、クライアント側では自動運転ロジックを完全には再現できていません。サーバーに依存せずに TrainUnit の図面／走行状態を再現するため、以下の観点で作業を進めます。

## 1. 状態同期の前提
- **Tick 付きイベント**: Docked / Departed 通知、ごとの `Tick` と `DiagramHash` を使用して順序保証と不一致検出を行う。
- **フルスナップショット**: `TrainUnitSnapshotBundle` を初期同期・再同期に使い、Diagram / Simulation / RailPosition を一度に整合させる。
- **Hash Verification**: クライアントでも `TrainDiagramHashCalculator` を使い、イベント適用後にデータ不整合がないか検証する。

## 2. 必要な機能タスク
1. **Diagram 完全キャッシュ**
   - `TrainUnitClient` が `TrainDiagramSnapshot` の Entries / CurrentIndex / 残り待機 tick を常に保持。
   - スナップショットの適用・差分更新を Tick ごとに反映する仕組みを追加。
2. **RailGraph + Station 情報**
   - `RailGraphClientCache` に駅ノード／StationId をキャッシュし、`ConnectionDestination` から駅ブロックを一意に特定できる状態を整える。
   - Dock 判定／StationRef 再現のための StationRegistry 相当をクライアントに持たせる。
3. **AutoRun シミュレーション**
   - サーバー側 `TrainUnit.Update()` の計算（Mascon、速度、距離、RailPosition 更新、`TrainUnitStationDocking` ロジック）を簡略化してクライアントへ移植。
   - Tick 駆動 Update を `ClientTrainUnit` へ追加し、`TrainUnitClientCache` から呼び出す。
4. **Dock/Depart イベント処理**
   - Tick 順序でキューに積んで処理する仕組みを作る。
   - イベント適用時に DiagramIndex を更新し、Docked 時には待機条件をリセットするなど Diagram と整合させる。
5. **RailPosition の逐次更新**
   - `RailPositionSaveData` 復元だけでなく、AutoRun Tick ごとに `RailPosition` を進め、クライアント描画 (`TrainRailObjectManager`) と同期できるようにする。
6. **UI／手動編集への拡張**
   - Diagram の UI 表示で利用できる API を整備。
   - 将来の手動編集要求（クライアント→サーバー）を想定し、編集結果を Snapshot に反映する経路を検討する。

## 3. テスト・検証
- `TrainDiagramUpdateTest` などのサーバー側テストに加え、クライアント用の単体テスト（Diagram Hash 検証 / イベント適用順序）を追加する。
- RailGraph Hash との組み合わせで「Docking → Depart → Snapshot 再同期」のシナリオを想定したテストケースを作成。
- WSL/macOS 等で `./tools/unity-test.sh moorestech_server "^Tests\.UnitTest\.Game\.TrainDiagramUpdateTest$"` を実行できる環境を確保し、CI でも確認する。

## 4. 今後の段階的進め方
1. Diagram Snapshot 適用ロジック／Hash 検証を完成させる。
2. RailGraph + Station 情報をクライアントに保持させ、Dock 判定をローカルで可能にする。
3. AutoRun Tick シミュレーションを導入し、Dock/Depart イベントを順序保証付きで適用する。
4. RailPosition 更新と描画連携を実装。
5. UI／手動編集への拡張、クライアント→サーバーの差分送信プロトコルを設計。

以上を満たすことで、クライアント側でも TrainUnit の自動運行を再現できる基盤が整います。***



## Client TrainUnit Implementation Notes (Update)
- Introduced ClientTrainDiagram to centralize TrainDiagramSnapshot reads, transitions, and UI access
  - UpdateSnapshot / UpdateIndexByEntryId
  - TryGetCurrentEntry / TryGetEntry / EntryCount
  - TryFindPathFrom performs "no destination -> advance to next entry" like the server
- ClientTrainUnit no longer caches diagramApproachingRailNode; destinations are resolved on demand
- DiagramHash verification uses ClientTrainDiagram.Snapshot
- Arrival handling on the client:
  - Station arrival waits for Docked/Departed events (speed stays at 0 while docked)
  - Non-station arrival advances to the next diagram entry locally
## Next Steps (Candidate)
1. Wire UI to ClientTrainDiagram read APIs for current entry and full entry list
2. Align AutoRun Dock/Depart transitions with server behavior (departure reset / arrival handling)
3. Build RailGraphClientCache and StationRegistry equivalent to stabilize destination node resolution
4. Add client-side tests for tick ordering and DiagramHash verification

## Client StationRef Priority
- ClientRailNode.StationRef is currently null; station/ non-station arrival cannot be distinguished accurately.
- This blocks correct Docked/Departed alignment and non-station-only local advancement.
- Priority: define how StationRef (or equivalent) is resolved on the client side.

## TrainUnit Hash Sync (Implemented)
- GetTrainUnitSnapshots now returns UnitsHash alongside ServerTick.
- Server broadcasts TrainUnit hash state events at a fixed interval:
  - Event tag: va:event:trainUnitHashState
  - Payload: UnitsHash + TrainTick
- Client compares the hash against TrainUnitClientCache and requests full snapshots on mismatch.
- Stale ticks (older than LastServerTick) are ignored.
