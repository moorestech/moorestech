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
| セーブ/ロード | `Tests/UnitTest/Game/SaveLoad/TrainRailSaveLoadTest.cs` | 統合テスト | レール・駅の保存復元、接続状態とインベントリの保持 | 走行中列車・時刻同期・運行状態の復元は未カバー |
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
  - [ ] 先詰まりシナリオ: 先頭列車が駅で長時間停止し後続が手前ノードで待機するケース (未実装)
  - [ ] 星形/格子ネットワークでの長時間運転と衝突ゼロ検証 (未実装)
  - [ ] 衝突数・予約失敗リトライ・平均待機時間など指標Assertionの導入 (未実装)
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
  - [ ] 走行中・減速中・駅ドック状態を含む列車のセーブ/ロード再現 (未実装)
  - [ ] ManualClockを用いたセーブ/ロード時刻再現テスト (未実装)
  - [x] 駅・レールブロックの保存/復元とインベントリ保持 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainRailSaveLoadTest.cs`)
- **着手ポイント**:
  - `FakeClock/ManualClock`を注入し、セーブ直前の時刻・ステートを制御して再現性を確保。
  - セーブデータ読み込み後に位置差±ε内で一致することをアサートする比較ユーティリティを整備。

## 優先度3: B) 動的変更(レール編集・駅切替)の検証
- **目的**: 実プレイ中の路線編集や駅状態変更に伴う経路再探索の堅牢性を担保する。
- **理由**:
  - ビルド/管理系ゲームでは編成運行中に路線を編集するのが一般的で、グリッチは体験悪化に直結。
  - 経路再探索のタイミング差(即時/到達時)の動作を検証することで、現在の仕様を明確化し不具合を早期発見可能。
  - IRailGraphServiceを差し替えるFaçade導入と組み合わせれば、固定seedのグラフで安定再現が可能。
- **必要テストと実装状況**:
  - [ ] レール削除・追加時の経路再探索フロー検証 (未実装)
  - [ ] 駅OFF→ON切替時の再探索およびログ検証 (未実装)
  - [ ] IRailGraphService Façade差し替えによる動的変更テスト (未実装)
- **着手ポイント**:
  - レール削除・追加、駅OFF→ONの各操作をシナリオ化し、再探索結果とログにグリッチがないか確認。
  - 経路再探索時に発生する予約解放/再確保のメトリクスを計測し、許容範囲を明示する。

## 中長期でのフォロー
- **C) 長さ・速度のストレステスト**: 物理/ロジック境界の検証に有用だが、上記3項で基礎挙動が安定してから着手すると効率的。
  - [ ] エッジ長・列車長・速度極端値での停止位置ズレ検証 (未実装)
- **E) フェイルインジェクション**: ディフェンシブ実装を確認できる重要領域。ログ基盤やエラーハンドリングが整備された時点で拡充する。
  - [ ] 駅/分岐設定ミスの安全な失敗検証 (未実装)

## 補足: テスト実装のテクニック
- `FakeClock`/`ManualClock`のDI注入と`clock.Advance(dt)`によるtick制御で時間依存性を排除。
- `IRailGraphService` Façadeとseed固定のグラフ生成で、動的変更や再探索シナリオの安定再現を担保。
- デコレータによるロギング(本番はNo-Op)を活用し、フレーム単位の経路・予約・解放イベントを追跡。
- プロパティテスト(CSCheck/FsCheck)で路線グラフの不変量を自動検証し、ランダムケースの網羅を補完。
- メトリクス断言(衝突数、再探索回数、予約失敗率、最大待ち行列長)に閾値を設け、許容超過時は即Failでフレークを検出。

