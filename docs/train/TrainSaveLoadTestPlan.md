# Train Save/Load Regression Checklist

以下は列車関連のセーブ・ロード機能を改修した後に実施すべきテスト項目です。テストケースは優先度順ではなく、シナリオごとに整理しています。

## 設計上の堅牢性メモ
- **ノード接続は常に双方向で復元される:** `RailComponent.ConnectRailComponent` が front/back の反対側まで同距離で接続し、ロード時も `RailComponentUtility.RestoreRailComponents` が同メソッドを通して接続を再構築します。セーブデータには「接続先」「自ノードのfront/back」がペアで保存されているため、片方向だけが残ることはありません。
- **ノードはブロックに紐付いて保存される:** `RailGraphDatastore` の `ConnectionDestination` はブロック座標と component index をキーにしており、ブロックそのものが存在しない限りノードは復元されません。手動でブロックを削除したセーブをロードしても孤立ノードは生成されないため、接続漏れが発生しません。
- **ドッキング情報は一方向リンクで検証される:** `TrainUnitStationDocking.RestoreDockingFromSavedState` は各車両の `dockingblock` を参照しつつ、駅側の `CanDock` を再評価してから再登録します。駅が失われている・車両が削除されている場合は該当ハンドルだけが自然に破棄され、残りの列車状態は維持されます。
- **距離情報は固定小数 (int) で保存される:** セーブ時に `RailNode` 間の距離は整数化されるため、ロードしても位置計算に誤差が蓄積しません。走行中にセーブ→ロードを繰り返しても、`RailPosition` が保持する残距離とノード間距離が厳密に一致します。

## 1. 基本的な復元確認
- レールグラフと駅ブロックを含む最小構成の環境を構築し、1編成だけを配置した状態でセーブ。
- ゲームを再起動してロードし、列車の位置・向き・速度が保存前と一致していることを確認。
- AutoRun を有効化した状態でセーブ→ロードし、`TrainUnit.IsAutoRun` が維持されているか、`DiagramValidation()` が自動で再実行されているかをチェック。
- ✅ 自動テスト: `TrainStationDockingPersistenceTest.ReloadingRestoresDockedTrainState` でドッキング済み列車の復元とAutoRun継続を検証済み。【F:moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs†L32-L89】

## 2. ダイアグラムと発車条件
- ダイアグラムに複数エントリを追加し、WaitForTicks を含む複数条件を設定した状態でセーブ。
- ロード後に `_currentIndex` と `DiagramEntry.entryId` が維持され、WaitForTicks の残り tick 数が継続されることをアサート。
- エントリに紐づく RailNode がセーブロードで欠損した場合、そのエントリが自動削除され、他のエントリが破壊されないことを確認。
- ✅ 自動テスト: `TrainStationDockingPersistenceTest.MultipleTrainsPreserveStateAcrossSaveLoad` がダイアグラム進行と WaitForTicks 残量の整合性を保証。【F:moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs†L136-L216】

## 3. 車両・インベントリ
- 貨車のインベントリおよび燃料スロットに複数種類のアイテムを格納した状態でセーブ。
- ロード後、スロット順序を含めて完全に復元されること、および `TrainCar.SetItem` / `SetFuelItem` が期待通り呼ばれることを確認。
- アイテムが無いスロットが空のまま維持されることも検証。
- ✅ 自動テスト: `TrainStationDockingPersistenceTest` で貨車スロットの積載数・アイテムIDが一致することを検証済み。【F:moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs†L52-L88】【F:moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs†L199-L231】

## 4. ドッキング状態
- 複数列車が同一駅に重なった状態でセーブし、ロード後に正しい編成だけが駅へ再ドッキングされるかをチェック。
- 駅ブロックをロード前に破壊したケースでは、該当車両の `dockingblock` が null にリセットされ、残りの車両のドッキングが影響を受けないことを確認。
- 自動運転 OFF の列車がドッキングしていた場合、ロード後も OFF のまま留まっていること。
- ✅ 自動テスト: `TrainStationDockingPersistenceTest` が複列車のドッキング復元と破損 JSON 時の安全な Undock をカバー。【F:moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs†L90-L135】【F:moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs†L180-L215】

## 5. パフォーマンス・連続保存 これはかなり後でやる
- 大規模な編成 (例: 10 両以上) を複数配置した状態で連続セーブ/ロードを実施し、フレーム落ちや保存時間が極端に悪化しないことを計測。
- セーブ直前と直後に `TrainUpdateService` の登録列車数が一致していることをログで確認。
- ⚠️ 自動テスト: 現状は `TrainStationDockingPersistenceTest` の複列車ケースで基本的な一致のみ確認。パフォーマンス計測は未着手。

テスト自動化が難しい項目は、Unity エディタ上でのスモークテスト用シナリオを用意して手動確認を行います。
