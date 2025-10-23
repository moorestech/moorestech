# Train Integration Test Implementation Priorities

このドキュメントは、提示された統合テスト計画(A-E)のうち、列車システムに関わる実装優先度が特に高いものを整理したものです。また、現在`train`という名前が付いたテストファイルがどの種類のテストをカバーしているかを俯瞰できるようにしています。優先順位の判断基準は以下の観点に基づいています。

- 実プレイで頻出し、致命的な障害やユーザー体験の大きな劣化に直結するか。
- 現状のテストカバレッジで不足していそうなシナリオか。
- 後続のテストや機能検証の土台になるか。

## 既存テストカバレッジの俯瞰

| テストカテゴリ | 主なテストファイル | テストタイプ | カバーしている挙動 | 未カバー/補足メモ |
| --- | --- | --- | --- | --- |
| レールグラフ探索・ノード接続 | `Tests/UnitTest/Game/SimpleTrainTest.cs` | ユニットテスト | Dijkstra探索の正当性、ノード接続関係、ランダムケースでの最短経路探索 | 複雑なネットワーク上での多列車運用や予約競合は未検証 |
| レールコンポーネント配置/駅向き | `Tests/UnitTest/Game/SimpleTrainTestStation.cs` | ユニットテスト | レール同士の接続、駅ブロック向きごとのRailComponent配置確認、駅間距離検証 | 目視確認(Log)依存のテストが残っており自動アサートが不足 |
| RailPosition遷移 | `Tests/UnitTest/Game/SimpleTrainTestRailPosition.cs` | ユニットテスト | 長編成の前進/後退やReverse時のノードスタック維持 | ベクトル長の極端値や曲線半径の検証は未実施 |
| 列車走行ロジック | `Tests/UnitTest/Game/SimpleTrainTestUpdateTrain.cs` | シナリオテスト (長時間) | ループ線路での走行、目的地到達、Station経由での往復、複数駅間の自動運転 | ランダム生成依存で再現性が低く、並列列車・フェイルケースは未検証 |
| ドッキングと積み下ろし | `Tests/UnitTest/Game/TrainStationDockingItemTransferTest.cs` | 統合テスト | 駅・貨物プラットフォームでの積込/荷降ろし、占有時の第二列車拒否 | 待避線を含む複数列車連携、長距離移動を伴うシナリオは未実装 |
| 列車運行ダイアグラム | `Tests/UnitTest/Game/TrainDiagramUpdateTest.cs` | 機能テスト | ダイアグラムのノード削除時リセット、積載/空荷条件での出発制御、複数条件の併用 | 長距離ダイアグラムや複数列車共有での挙動は未検証 |
| 自動運転操作シナリオ | `Tests/UnitTest/Game/TrainDiagramAutoRunOperationsTest.cs` | 機能テスト (骨子) | 自動運転ダイアグラム操作のケース網羅を目的としたテスト構造 | 具体的なアサート未実装、シナリオ充実が今後の課題 |
| セーブ/ロード | `Tests/UnitTest/Game/SaveLoad/TrainRailSaveLoadTest.cs` | 統合テスト | レール・駅の保存復元、接続状態とインベントリの保持 | 走行中列車・時刻同期・運行状態・ダイアグラム進行・ドッキング占有は未カバー |
| シングルトレイン往復 | `Tests/UnitTest/Game/SingleTrainTwoStationIntegrationTest.cs` | シナリオテスト | 2駅間での積込→運搬→荷降ろし→往復完走、手動スイッチ操作を含む | 長時間運転や複数列車・ポイント切替は未検証 |

### 現行`train`関連テストファイル一覧
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SimpleTrainTest.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SimpleTrainTestStation.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SimpleTrainTestRailPosition.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SimpleTrainTestUpdateTrain.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainDiagramUpdateTest.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainDiagramAutoRunOperationsTest.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStationDockingItemTransferTest.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainRailSaveLoadTest.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SingleTrainTwoStationIntegrationTest.cs`
- (補助ユーティリティ) `moorestech_server/Assets/Scripts/Tests/Util/TrainTestHelper.cs`
- (補助ユーティリティ) `moorestech_server/Assets/Scripts/Tests/Util/TrainAutoRunTestScenario.cs`

### 各テストファイルの概要
- **SimpleTrainTest.cs**: RailNodeの経路探索と接続性を検証し、複雑グラフやランダム生成ケースでの最短経路の健全性を確かめる。 
- **SimpleTrainTestStation.cs**: 駅ブロックの向きに応じたRailComponent配置や駅間距離の整合性、手動接続時の最短経路を検証する。 
- **SimpleTrainTestRailPosition.cs**: 長編成列車の前進・後退およびReverse処理時にノードスタックが正しく更新されるかをテストする。 
- **SimpleTrainTestUpdateTrain.cs**: 自動運転やループ走行などのシナリオを通じて、TrainUnitの移動・目的地処理・分割挙動を網羅的に検証する長時間テスト。 
- **TrainDiagramUpdateTest.cs**: 列車ダイアグラムのノード削除、条件設定、待機ティックなどの管理処理が正しく機能するかを確認する。 
- **TrainDiagramAutoRunOperationsTest.cs**: 自動運転ダイアグラム操作のテストケース群の雛形を提供し、今後の詳細アサート追加の受け皿となる。 
- **TrainStationDockingItemTransferTest.cs**: 駅・貨物プラットフォームでの積載／荷降ろしおよび占有制御が正しく働くかを統合的に確認する。 
- **TrainRailSaveLoadTest.cs**: レールや駅のセーブデータ復元、接続情報・インベントリ状態の永続化が機能するかを検証する。 
- **SingleTrainTwoStationIntegrationTest.cs**: 二駅間の積込→運搬→荷降ろし→折り返しという往復ループが自動運転で完了することを確認する。 
- **TrainTestHelper.cs / TrainAutoRunTestScenario.cs**: 上記テストで使用するテスト環境・シナリオ構築ユーティリティを提供するサポートコード。

### カバレッジ詳細メモ
- **ユニット層**: RailGraph/RailPosition関連のアルゴリズム系テスト(`SimpleTrainTest*.cs`)が存在し、基礎的な計算ロジックは網羅している。
- **統合層**: ドッキング/積み下ろし(`TrainStationDockingItemTransferTest.cs`)とセーブ/ロード(`TrainRailSaveLoadTest.cs`)は、単列車または静的環境下での正当性を担保する。運行状態の保存や動的な線路変更は未カバー。
- **シナリオ層**: `SimpleTrainTestUpdateTrain.cs`と`SingleTrainTwoStationIntegrationTest.cs`が単列車シナリオをカバー。ただしランダム性が高いものが多く、長時間耐久やマルチトレイン競合は未実装。

> 📌 **ギャップまとめ**: 「複数列車が同一路線を共有する長時間運行」「路線編集や駅状態変化を伴う再探索」「走行中セーブ/ロード」「フェイルインジェクション」などは既存テストで扱われていないため、本書の優先事項を満たす追加実装が必要。

## 優先度1: A) 多列車シナリオの統合テスト強化
- **目的**: デッドロックや衝突の未検出を防ぎ、複数列車運用の基礎品質を保証する。
- **理由**:
  - 実プレイで最も頻発するケースであり、異常時の影響(衝突・詰まり)が致命的。
  - 現在のテストでは複数列車の継続運行や複雑ネットワークの耐久性を十分にカバーできていない可能性が高い。
  - 星形/格子など高分岐ネットワークでの長時間シミュレーションと指標Assertionは、後続のシナリオ全般の信頼性を底上げする。
- **必要テストと実装状況**:
  - [ ] 交換・待避・復帰の一連挙動を網羅する多列車シナリオ (未実装)
  - [ ] 先詰まりシナリオ: 先頭列車が駅で長時間停止し後続が同ノードでドッキングまち待機するケース (未実装)
  - [ ] 星形/格子ネットワークでの長時間運転検証 (未実装)
  - [x] 単列車×二駅の往復テスト (駅1積載→駅2荷降ろし) と手動スイッチ連携 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SingleTrainTwoStationIntegrationTest.cs`) — シナリオテストとして単列車の往復を自動検証
  - [x] 単駅での積み込み/荷降ろし切替確認 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStationDockingItemTransferTest.cs`)
  - [x] 駅占有時の後続列車待機(二列車)の衝突防止 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStationDockingItemTransferTest.cs`)
- **着手ポイント**:
  - 交換・待避・復帰の一連挙動を網羅するシナリオを`TrainTestHelper`ベースで構築。
  - 単列車×二駅の往復テストを追加し、駅1での積載→駅2での荷降ろしまで自動検証できる状態にする。
    - 貨物プラットフォームのロード/アンロード切替スイッチを手動操作するテストフックを用意し、図面(diagram)の出発条件と連携させる。
  - 衝突数・予約失敗リトライ・平均待機時間のメトリクス断言を追加し、フレーク検出を行う。

## 優先度2: D) セーブ/ロードの実ゲーム相当検証
- **目的**: セーブ/ロード後の状態再現性を保証し、長時間プレイの信頼性を確保する。
- **理由**:
  - プレイヤーが日常的に行う操作であり、破綻すると進行不能バグにつながる。
  - 走行・減速・駅ドックなど複数状態の再現は、他システム(時刻管理・予約・ドッキング)の回帰も検知できる。
  - 偏差許容を定義しておけば自動テストとして安定化し、将来のリグレッションを早期に捕捉できる。
- **必要テストと実装状況**:
  - [ ] 走行中・減速中・駅ドック状態を含む複数列車のセーブ/ロード再現 (未実装)
    - 位置(ノードと残距離)、速度、加速度、編成構成、牽引順序、カーブ進入方向が一致することを検証。
    - 自動運転中・手動運転中双方を含め、ロード後にダイアグラム再開が継続することを確認。
  - [ ] TrainUnit dock状態の堅牢性検証 (未実装)
    - ドック中の編成を解体/削除してセーブ→ロードした際に駅が自然解除され、孤立ハンドルが残らないことを検証。
    - 逆にセーブ→ロードした際に駅を削除し、ドッキング解除が自動で行われるか検証
  - [ ] 貨物列車インベントリとダイアグラム設定の永続化 (未実装)
    - 積載アイテム数・スロット順序、`DiagramSchedule`/`AutoRunCondition`の設定値が完全一致することを比較。
    - 車両ごとのCargoBayの残容量・予約状態が正しく再現されるかをチェック。
  - [ ] 破損セーブデータに対するフォールバック動作 (未実装)
    - Node接続片側欠損、Docking先欠損、ダイアグラム参照欠損などを人工的に挿入し、堅牢性チェックが発動してゲーム継続可能な状態になるかを確認。
  - [x] 駅・レールブロックの保存/復元とインベントリ保持 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainRailSaveLoadTest.cs`)
- **着手ポイント**:
  - `TrainAutoRunTestScenario`を拡張し、複数列車/複数駅の状態を簡潔にセットアップできるビルダーを用意。
  - Docking再構築時のログ/イベントを捕捉し、異常検知が走った場合にもテストで意図通りRecoverできているか確認。

### Docking／TrainUnit解体周りの個別課題

- **目的**: セーブ/ロードに絡む片方向参照や手動削除を行っても、駅・列車双方の状態が破綻しないことを保証する。
- **主なテスト候補**:
  - [ ] StationDockingServiceがロード時にTrainUnitの不整合を検知・解放することを検証するユニットテスト。
  - [ ] TrainUnitのDispose/Destroyが呼ばれたときに駅占有が確実に解除されることを確認するテスト。必要であれば`TrainUnit`のデストラクタ処理に対するユニットテストを追加。


