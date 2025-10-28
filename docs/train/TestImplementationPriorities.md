# Train Integration Test Implementation Priorities

このドキュメントは、提示された統合テスト計画(A-E)のうち、列車システムに関わる実装優先度が特に高いものを整理したものです。また、現在`train`という名前が付いたテストファイルがどの種類のテストをカバーしているかを俯瞰できるようにしています。優先順位の判断基準は以下の観点に基づいています。

- 実プレイで頻出し、致命的な障害やユーザー体験の大きな劣化に直結するか。
- 現状のテストカバレッジで不足していそうなシナリオか。
- 後続のテストや機能検証の土台になるか。

## 直近の実装状況 (2024-05-28更新)

- ✅ **セーブデータ破壊テスト基盤を整備** — `SaveLoadJsonTestHelper` にセーブ→破壊→ロードを一括で行う `SaveCorruptAndLoad` や `RemoveTrainUnitDockedAt` などのユーティリティを追加し、列車テストからフェイルインジェクションを直接呼び出せるようになりました。【F:moorestech_server/Assets/Scripts/Tests/Util/SaveLoadJsonTestHelper.cs†L1-L118】【F:moorestech_server/Assets/Scripts/Tests/Util/SaveLoadJsonTestHelper.cs†L144-L192】
- ✅ **ドッキング整合性テストを拡充** — 駅占有解除・破棄時の安全性・破損JSONロード時の挙動を `TrainStationDockingPersistenceTest` で自動検証し、`TrainStationDockingScenario` でのセットアップを共通化しました。【F:moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs†L1-L207】【F:moorestech_server/Assets/Scripts/Tests/Util/TrainStationDockingScenario.cs†L1-L170】
- ✅ **複列車セーブ/ロード回帰テストを追加** — 複数列車の状態・ダイアグラム・インベントリ・WaitForTicks残量をスナップショット比較で確認する統合テストを実装し、ロード後の完全一致を担保しています。【F:moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs†L136-L216】
- ✅ **セーブ/ロード後のグラフ構造・速度・巨大シナリオを自動検証** — レール構造の完全一致を `RailGraphSaveLoadConsistencyTest`、速度維持を `TrainSpeedSaveLoadTest`、1200レール×7列車の耐久シナリオを `HugeAutoRunTrainSaveLoadConsistencyTest` が担保するようになり、長時間運用や高負荷ケースの回帰が即座に検出できます。【F:moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/RailGraphSaveLoadConsistencyTest.cs†L1-L78】【F:moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainSpeedSaveLoadTest.cs†L1-L64】【F:moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/HugeAutoRunSaveLoadConsistencyTest.cs†L1-L83】

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
| セーブ/ロード (ブロック) | `Tests/UnitTest/Game/SaveLoad/TrainRailSaveLoadTest.cs` | 統合テスト | レール・駅の保存復元、接続状態とインベントリの保持 | 列車運行中の状態やドッキング継続までは未検証 |
| セーブ/ロード (レールグラフ) | `Tests/UnitTest/Game/SaveLoad/RailGraphSaveLoadConsistencyTest.cs` | 統合テスト | セーブ前後でRailGraphのノード・エッジ構造が完全一致することを検証 | 大規模グラフでの性能測定は未実施 |
| セーブ/ロード (列車・ドッキング) | `Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs` | 統合テスト | ドッキング状態の復元、破損JSONロード時のフォールバック、複列車状態の整合性 | 長距離路線・ポイント切替を含む多列車運行までは未検証 |
| セーブ/ロード (ダイアグラム) | `Tests/UnitTest/Game/SaveLoad/TrainDiagramSaveLoadTest.cs` | 統合テスト | ダイアグラムエントリ・待機Tick・欠損ノード時のスキップ処理を検証 | 巨大ダイアグラムや複数列車共有ケースは未検証 |
| セーブ/ロード (走行速度) | `Tests/UnitTest/Game/SaveLoad/TrainSpeedSaveLoadTest.cs` | 統合テスト | 高速走行中の列車速度がセーブ後も一致するかを確認 | 連結列車や複数列車同時計測は未検証 |
| セーブ/ロード (自動運転長時間) | `Tests/UnitTest/Game/SaveLoad/HugeAutoRunSaveLoadConsistencyTest.cs` | 長時間シナリオテスト | 1200本のレールと7列車による自動運転シナリオでセーブ有無のスナップショット一致を比較 | さらなる桁の負荷や乱数シード違いは未検証 |
| ドッキング同時実行 | `Tests/UnitTest/Game/TrainStationDockingConcurrencyTest.cs` | 機能テスト | 前後両方向からの同時ドッキングや占有解除の競合を検証 | 長時間連続運転時の競合や多数列車の同時接近は未検証 |
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
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainRailGraphSaveLoadConsistencyTest.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainDiagramSaveLoadTest.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainSpeedSaveLoadTest.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainHugeAutoRunSaveLoadConsistencyTest.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStationDockingConcurrencyTest.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainSingleTwoStationIntegrationTest.cs`
- (補助ユーティリティ) `moorestech_server/Assets/Scripts/Tests/Util/TrainTestHelper.cs`
- (補助ユーティリティ) `moorestech_server/Assets/Scripts/Tests/Util/TrainAutoRunTestScenario.cs`
- (補助ユーティリティ) `moorestech_server/Assets/Scripts/Tests/Util/TrainStationDockingScenario.cs`
- (補助ユーティリティ) `moorestech_server/Assets/Scripts/Tests/Util/RailGraphNetworkTestHelper.cs`

### 各テストファイルの概要
- **SimpleTrainTest.cs**: RailNodeの経路探索と接続性を検証し、複雑グラフやランダム生成ケースでの最短経路の健全性を確かめる。 
- **SimpleTrainTestStation.cs**: 駅ブロックの向きに応じたRailComponent配置や駅間距離の整合性、手動接続時の最短経路を検証する。 
- **SimpleTrainTestRailPosition.cs**: 長編成列車の前進・後退およびReverse処理時にノードスタックが正しく更新されるかをテストする。 
- **SimpleTrainTestUpdateTrain.cs**: 自動運転やループ走行などのシナリオを通じて、TrainUnitの移動・目的地処理・分割挙動を網羅的に検証する長時間テスト。 
- **TrainDiagramUpdateTest.cs**: 列車ダイアグラムのノード削除、条件設定、待機ティックなどの管理処理が正しく機能するかを確認する。 
- **TrainDiagramAutoRunOperationsTest.cs**: 自動運転ダイアグラム操作のテストケース群の雛形を提供し、今後の詳細アサート追加の受け皿となる。 
- **TrainStationDockingItemTransferTest.cs**: 駅・貨物プラットフォームでの積載／荷降ろしおよび占有制御が正しく働くかを統合的に確認する。 
- **TrainStationDockingPersistenceTest.cs**: ドッキング状態の保存・復元、破損セーブからのフェイルセーフ、複列車スナップショット比較で列車セーブデータの整合性を検証する。
- **TrainStationDockingConcurrencyTest.cs**: 前後方向やループ構造で複数列車が同時に駅へ進入するケースを再現し、占有解除と再ドックの競合を確認する。
- **TrainRailSaveLoadTest.cs**: レールや駅のセーブデータ復元、接続情報・インベントリ状態の永続化が機能するかを検証する。
- **TrainRailGraphSaveLoadConsistencyTest.cs**: 複数レールを配置したグラフをセーブ・ロードし、ノード接続や距離情報が完全一致することをスナップショット比較で確認する。
- **TrainDiagramSaveLoadTest.cs**: ダイアグラムエントリの復元と欠損ノードのスキップ処理を検証し、ロード後の条件・待機Tickが破綻しないことを確認する。
- **TrainSingleTwoStationIntegrationTest.cs**: 二駅間の積込→運搬→荷降ろし→折り返しという往復ループが自動運転で完了することを確認する。
- **TrainSpeedSaveLoadTest.cs**: 高速走行中の列車をセーブ・ロードしても `TrainUnit.CurrentSpeed` が一致することをリフレクション設定を用いて検証する。
- **TrainHugeAutoRunSaveLoadConsistencyTest.cs**: 数千ノード規模のレール網と多数列車の自動運転シナリオで、セーブ有無の結果スナップショットが一致するかを長時間シミュレーションで比較する。
- **TrainTestHelper.cs / TrainAutoRunTestScenario.cs / TrainStationDockingScenario.cs**: 上記テストで使用するテスト環境・シナリオ構築ユーティリティを提供し、列車・駅セットアップを簡潔化するサポートコード。
- **RailGraphNetworkTestHelper.cs**: RailComponent集合からノード/エッジ構造をスナップショット化し、ロード後のRailGraphとの完全一致を比較するユーティリティ。

### カバレッジ詳細メモ
- **ユニット層**: RailGraph/RailPosition関連のアルゴリズム系テスト(`SimpleTrainTest*.cs`)が存在し、基礎的な計算ロジックは網羅している。
- **統合層**: ドッキング/積み下ろし(`TrainStationDockingItemTransferTest.cs`)に加えて、破損セーブや複列車復元を扱う`TrainStationDockingPersistenceTest.cs`、ブロック保存を扱う`TrainRailSaveLoadTest.cs`が揃い、駅占有の安全性やWaitForTicks復元まで自動検証できるようになった。
- **シナリオ層**: `SimpleTrainTestUpdateTrain.cs`と`SingleTrainTwoStationIntegrationTest.cs`が単列車シナリオを、`TrainStationDockingConcurrencyTest.cs`が前後列車の競合パターンを担保する。ただしランダム性が高いものが多く、長時間耐久や大規模路線でのマルチトレイン競合は未実装。

> 📌 **ギャップまとめ**: 「複数列車が同一路線を共有する長時間運行しているときの路線編集や駅状態変化を伴う再探索」などは引き続き未カバーのため、今後の優先課題として残る。

## 優先度1: A) 多列車シナリオの統合テスト強化
- **目的**: デッドロックの未検出を防ぎ、複数列車運用の基礎品質を保証する。
- **理由**:
  - 実プレイで最も頻発するケースであり、異常時の影響(衝突・詰まり)が致命的。
  - 星形/格子など高分岐ネットワークでの長時間シミュレーションと指標Assertionは、後続のシナリオ全般の信頼性を底上げする。->TrainHugeAutoRunSaveLoadConsistencyTest.csである程度カバー
- **必要テストと実装状況**:
  - [ ] 交換・待避・復帰の一連挙動を網羅する多列車シナリオ (未実装)
  - [x] 星形/格子ネットワークでの長時間運転検証 (moorestech_server\Assets\Scripts\Tests\UnitTest\Game\SaveLoad\TrainHugeAutoRunTrainSaveLoadConsistencyTest.cs)
  - [x] 先詰まり・後続待機シナリオ (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStationDockingConcurrencyTest.cs`) — 先頭列車が駅を占有した状態で後続が安全に待機するかを検証
  - [x] ループ構造での超長編成ドッキング検証 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStationDockingConcurrencyTest.cs`)
  - [x] 単列車×二駅の往復テスト (駅1積載→駅2荷降ろし) と手動スイッチ連携 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainSingleTwoStationIntegrationTest.cs`) — シナリオテストとして単列車の往復を自動検証
  - [x] 単駅での積み込み/荷降ろし切替確認 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStationDockingItemTransferTest.cs`)
- **着手ポイント**:
  - 交換・待避・復帰の一連挙動を網羅するシナリオを`TrainTestHelper`/`TrainStationDockingScenario`ベースで構築。

## 優先度2: D) セーブ/ロードの実ゲーム相当検証
- **目的**: セーブ/ロード後の状態再現性を保証し、長時間プレイの信頼性を確保する。
- **理由**:
  - プレイヤーが日常的に行う操作であり、破綻すると進行不能バグにつながる。
  - 走行・減速・駅ドックなど複数状態の再現は、他システム(時刻管理・予約・ドッキング)の回帰も検知できる。
  - 偏差許容を定義しておけば自動テストとして安定化し、将来のリグレッションを早期に捕捉できる。
- **必要テストと実装状況**:
  - [x] 走行中・減速中・駅ドック状態を含む複数列車のセーブ/ロード再現 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs`)
    - 位置・速度・残距離・自動運転状態・`WaitForTicks`の残量をスナップショット比較し、ロード後に完全一致することを検証済み。
  - [x] TrainUnit dock状態の堅牢性検証 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs`)
    - 列車破棄時の駅占有解除、およびドッキング先ブロック欠損時の安全なUndockを自動テストで確認。
  - [x] 貨物列車インベントリとダイアグラム設定の永続化 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs`)
    - 貨物スロットの積載数とダイアグラム進行状態(現在ノード・WaitForTicks残量)がロード後も一致することを検証。
  - [x] 破損セーブデータに対するフォールバック動作 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs`)
    - `SaveLoadJsonTestHelper.SaveCorruptAndLoad` を用い、DockingBlockPosition欠損時に自動的にUndockして再接続可能なことを確認。
  - [x] 駅・レールブロックの保存/復元とインベントリ保持 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainRailSaveLoadTest.cs`)
- **着手ポイント**:
  - `TrainAutoRunTestScenario`/`TrainStationDockingScenario`を拡張し、曲線やポイント切替を含む路線でのセーブ/ロードケースを量産できるようにする。
  - `SaveLoadJsonTestHelper` の破壊ユーティリティを拡張し、ノード接続欠損やダイアグラム参照欠損など多様なフェイルインジェクションをテンプレ化する。

### Docking／TrainUnit解体周りの個別課題

- **目的**: セーブ/ロードに絡む片方向参照や手動削除を行っても、駅・列車双方の状態が破綻しないことを保証する。
- **主なテスト候補**:
  - [ ] StationDockingServiceがロード時にTrainUnitの不整合を検知・解放することを検証するユニットテスト。(未実装)
  - [x] TrainUnitのDispose/Destroyが呼ばれたときに駅占有が確実に解除されることを確認するテスト (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs`)。


