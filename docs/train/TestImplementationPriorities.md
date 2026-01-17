# Train Integration Test Implementation Priorities

列車システムに関わる統合テストの実装状況と優先度をまとめたドキュメントです。`train` 系のテストファイルがどの挙動をカバーし、どこが未検証なのかを俯瞰できるように整理しています。優先度は以下の観点で判断しています。

- 実プレイで頻発し、致命的な障害や体験劣化に直結するか  
- 現状のテストカバレッジで不足していそうなシナリオか  
- 後続テストや機能検証の土台になるか  

## 直近の実装状況 (2026-01-15更新)

- ✅ **列車反転テストを追加** — `TrainUnitReverseTest` で `TrainUnit.Reverse()` が編成順・車両向きを正しく反転し、牽引力計算が更新されることを確認。  
- ✅ **双方向貨物ループのセーブデータテストを追加** — `TrainBidirectionalCargoLoopSaveDataTest` で逆方向に走る 2 編成が両駅で積み込み／荷降ろしを完了し、セーブデータに車両数・向きが保存されることを検証。  
- ✅ **セーブデータ破壊テスト基盤を整備** — `SaveLoadJsonTestHelper` に `SaveCorruptAndLoad` や `RemoveTrainUnitDockedAt` などを追加し、列車テストからフェイルインジェクションを直接実行可能に。  
- ✅ **ドッキング整合性テストを拡充** — 駅占有解除・破棄時の安全性・破損 JSON ロード時の挙動を `TrainStationDockingPersistenceTest` で自動検証し、`TrainStationDockingScenario` でセットアップを共通化。  
- ✅ **複列車セーブ/ロード回帰テストを追加** — 複数列車の状態・ダイアグラム・インベントリ・`WaitForTicks` 残量をスナップショット比較し、ロード後の完全一致を担保。  
- ✅ **セーブ/ロード後のグラフ構造・速度・巨大シナリオを自動検証** — レール構造の完全一致を `TrainRailGraphSaveLoadConsistencyTest`、速度維持を `TrainSpeedSaveLoadTest`、1200 レール×7 列車の耐久シナリオを `TrainHugeAutoRunSaveLoadConsistencyTest` が検知。  
- ✅ **ダイヤグラム自動運転操作テストを拡充** — `TrainDiagramAutoRunOperationsTest` でダイヤグラムのエントリ削除時における自動運転継続/解除動作を検証するケースを実装。  
- ✅ **車両増結テストを追加** — `TrainUnitAddCarTest` で `TrainUnit.AttachCarToHead()` / `AttachCarToRear()` を用いて編成増結を行った際に、編成長・ノード位置・車両向きが正しく更新されることを確認。  

## 既存テストカバレッジの俯瞰

| テストカテゴリ                     | 主なテストファイル                                            | テストタイプ     | カバーしている挙動                                                                                         | 未カバー / 補足メモ                                               |
| ---------------------------------- | ------------------------------------------------------------ | ---------------- | -------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| レールグラフ探索・ノード接続         | `Tests/UnitTest/Game/SimpleTrainTest.cs`                      | ユニットテスト     | Dijkstra 探索の正当性、ノード接続関係、ランダムケースでの最短経路探索                                       | 複雑ネットワーク上の多列車運用や予約競合は未検証                   |
| レールコンポーネント配置/駅向き      | `Tests/UnitTest/Game/SimpleTrainTestStation.cs`               | ユニットテスト     | レール同士の接続、駅ブロック向きごとの `RailComponent` 配置確認、駅間距離検証                               | 目視確認(Log)依存のテストが残っており自動アサートが不足           |
| `RailPosition` 遷移               | `Tests/UnitTest/Game/SimpleTrainTestRailPosition.cs`          | ユニットテスト     | 長編成の前進/後退や Reverse 時のノードスタック維持                                                         | ベクトル長の極端値や曲線半径の検証は未実施                       |
| 列車走行ロジック                   | `Tests/UnitTest/Game/SimpleTrainTestUpdateTrain.cs`           | シナリオテスト (長時間) | ループ線路での走行、目的地到達、駅経由での往復、複数駅間の自動運転                                         | ランダム生成依存で再現性が低く、並列列車・フェイルケースは未検証   |
| ドッキングと積み下ろし             | `Tests/UnitTest/Game/TrainStationDockingItemTransferTest.cs`  | 統合テスト         | 駅・貨物プラットフォームでの積込/荷降ろし、占有時の第二列車拒否                                           | 待避線を含む複数列車連携、長距離移動を伴うシナリオは未実装       |
| ドッキング同時実行                 | `Tests/UnitTest/Game/TrainStationDockingConcurrencyTest.cs`   | 機能テスト         | 前後両方向からの同時ドッキングや占有解除の競合、ループ構造での長編成ドックを検証                           | 長時間連続運転時の競合や多数列車の同時接近は未検証               |
| 列車反転処理                       | `Tests/UnitTest/Game/TrainUnitReverseTest.cs`                 | ユニットテスト     | `TrainUnit.Reverse()` で編成順が反転し、車両向きと牽引力計算が更新されることを検証するユニットテスト。 | 走行中の反転や多両編成でのトラクション再計算は未検証             |
| **列車増結処理**                   | `Tests/UnitTest/Game/TrainUnitAddCarTest.cs`                  | ユニットテスト     | `TrainUnit.AttachCarToHead()` / `AttachCarToRear()` による編成増結時に編成長・ノード位置・車両向きが正しく更新されることを検証 | 自動運転中の増解結やセーブデータ整合性検証は未カバー             |
| 双方向貨物ループ & 保存            | `Tests/UnitTest/Game/SaveLoad/TrainBidirectionalCargoLoopSaveDataTest.cs` | シナリオテスト / セーブ | 2 つの貨物駅間を逆方向に走る 2 編成が積み込み／荷降ろしを循環し、セーブデータに車両数・向きが記録されることを検証 | 複数両編成や複数貨物種類、長距離区間でのループは未検証           |
| 列車運行ダイアグラム               | `Tests/UnitTest/Game/TrainDiagramUpdateTest.cs`               | 機能テスト         | ダイアグラムのノード削除時リセット、積載/空荷条件での出発制御、複数条件の併用                               | 長距離ダイアグラムや複数列車共有での挙動は未検証                 |
| 自動運転操作シナリオ               | `Tests/UnitTest/Game/TrainDiagramAutoRunOperationsTest.cs`    | 機能テスト         | ダイヤグラムの現在/非現在エントリ削除、および全エントリ削除時の自動運転継続/停止動作を確認                 | エントリ削除のケースは実装済み、さらなる操作シナリオ網羅は今後の課題 |
| セーブ/ロード (レールグラフ)        | `Tests/UnitTest/Game/SaveLoad/TrainRailGraphSaveLoadConsistencyTest.cs` | 統合テスト         | セーブ前後で `RailGraph` のノード・エッジ構造が完全一致することを検証                                       | 大規模グラフでの性能測定は未実施                                 |
| セーブ/ロード (列車・ドッキング)    | `Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs` | 統合テスト         | ドッキング状態の復元、破損 JSON ロード時のフォールバック、複列車状態の整合性                               | 長距離路線・ポイント切替を含む多列車運行までは未検証             |
| セーブ/ロード (ダイアグラム)        | `Tests/UnitTest/Game/SaveLoad/TrainDiagramSaveLoadTest.cs`    | 統合テスト         | ダイアグラムエントリ・待機 Tick・欠損ノード時のスキップ処理を検証                                           | 巨大ダイアグラムや複数列車共有ケースは未検証                     |
| セーブ/ロード (走行速度)           | `Tests/UnitTest/Game/SaveLoad/TrainSpeedSaveLoadTest.cs`      | 統合テスト         | 高速走行中の列車速度がセーブ後も一致するかを確認                                                           | 連結列車や複数列車同時計測は未検証                               |
| セーブ/ロード (自動運転長時間)      | `Tests/UnitTest/Game/SaveLoad/TrainHugeAutoRunSaveLoadConsistencyTest.cs` | 長時間シナリオテスト | 1200 本のレールと多数列車でセーブ有無の結果スナップショット一致を比較                                       | さらなる桁の負荷や乱数シード違いは未検証                         |
| シングルトレイン往復               | `Tests/UnitTest/Game/TrainSingleTwoStationIntegrationTest.cs` | シナリオテスト       | 2 駅間での積込→運搬→荷降ろし→往復完走を自動検証                                                           | 長時間運転や複数列車・ポイント切替は未検証                       |

## 現行 `train` 関連テストファイル一覧

- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SimpleTrainTest.cs`  
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SimpleTrainTestStation.cs`  
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SimpleTrainTestRailPosition.cs`  
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SimpleTrainTestUpdateTrain.cs`  
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainUnitReverseTest.cs`  
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainUnitAddCarTest.cs`  
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStationDockingItemTransferTest.cs`  
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStationDockingConcurrencyTest.cs`  
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainDiagramUpdateTest.cs`  
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainDiagramAutoRunOperationsTest.cs`  
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainSingleTwoStationIntegrationTest.cs`  
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainRailGraphSaveLoadConsistencyTest.cs`  
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs`  
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainDiagramSaveLoadTest.cs`  
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainSpeedSaveLoadTest.cs`  
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainHugeAutoRunSaveLoadConsistencyTest.cs`  
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainBidirectionalCargoLoopSaveDataTest.cs`  
- (補助ユーティリティ) `moorestech_server/Assets/Scripts/Tests/Util/TrainTestHelper.cs`  
- (補助ユーティリティ) `moorestech_server/Assets/Scripts/Tests/Util/TrainAutoRunTestScenario.cs`  
- (補助ユーティリティ) `moorestech_server/Assets/Scripts/Tests/Util/TrainStationDockingScenario.cs`  
- (補助ユーティリティ) `moorestech_server/Assets/Scripts/Tests/Util/RailGraphNetworkTestHelper.cs`  
- (補助ユーティリティ) `moorestech_server/Assets/Scripts/Tests/Util/SaveLoadJsonTestHelper.cs`  

## 各テストファイルの概要

- **SimpleTrainTest.cs**: `RailNode` の経路探索と接続性を検証し、複雑グラフやランダム生成ケースでの最短経路の健全性を確かめる。  
- **SimpleTrainTestStation.cs**: 駅ブロックの向きに応じた `RailComponent` 配置や駅間距離の整合性、手動接続時の最短経路を検証する。  
- **SimpleTrainTestRailPosition.cs**: 長編成列車の前進・後退および `TrainUnit.Reverse()` 時にノードスタックが正しく更新されるかをテストする。  
- **SimpleTrainTestUpdateTrain.cs**: 自動運転やループ走行などのシナリオを通じ、`TrainUnit` の移動・目的地処理・分割挙動を網羅的に検証する長時間テスト。  
- **TrainUnitReverseTest.cs**: `TrainUnit.Reverse()` が編成順・車両向きを入れ替え、牽引力計算も正しく更新されることを検証するユニットテスト。  
- **TrainUnitAddCarTest.cs**: `TrainUnit.AttachCarToHead()` / `AttachCarToRear()` を用いて編成増結を行った際に、編成長・ノード位置・車両向きが正しく更新されることを確認する。  
- **TrainStationDockingItemTransferTest.cs**: 駅・貨物プラットフォームでの積載／荷降ろしおよび占有制御が正しく働くかを統合的に確認する。  
- **TrainStationDockingConcurrencyTest.cs**: 前後方向やループ構造で複数列車が同時に駅へ進入するケースを再現し、占有解除と再ドックの競合を確認する。  
- **TrainDiagramUpdateTest.cs**: 列車ダイアグラムのノード削除、条件設定、待機 Tick などの管理処理が正しく機能するかを確認する。  
- **TrainDiagramAutoRunOperationsTest.cs**: 自動運転ダイヤグラム操作のテストケース群の雛形を提供し、今後の詳細アサート追加の受け皿となる。  
- **TrainRailGraphSaveLoadConsistencyTest.cs**: 複数レールを配置したグラフをセーブ・ロードし、ノード接続や距離情報が完全一致することをスナップショット比較で確認するユーティリティ。  
- **TrainDiagramSaveLoadTest.cs**: ダイアグラムエントリの復元と欠損ノードのスキップ処理を検証し、ロード後の条件・待機 Tick が破綻しないことを確認する。  
- **TrainStationDockingPersistenceTest.cs**: ドッキング状態の保存・復元、破損セーブからのフェイルセーフ、複列車スナップショット比較で列車セーブデータの整合性を検証する。  
- **TrainSpeedSaveLoadTest.cs**: 高速走行中の列車をセーブ・ロードしても `TrainUnit.CurrentSpeed` が一致することをリフレクション設定を用いて検証する。  
- **TrainHugeAutoRunSaveLoadConsistencyTest.cs**: 数千ノード規模のレール網と多数列車の自動運転シナリオで、セーブ有無の結果スナップショットが一致するかを長時間シミュレーションで比較する。  
- **TrainBidirectionalCargoLoopSaveDataTest.cs**: 2 つの貨物駅と 2 編成の列車を用いて逆方向の貨物ループが循環すること、およびセーブデータに列車の向きと台数が正しく保存されることを検証する。  
- **TrainSingleTwoStationIntegrationTest.cs**: 二駅間の積込→運搬→荷降ろし→折り返しという往復ループが自動運転で完了することを確認する。  
- **TrainTestHelper.cs / TrainAutoRunTestScenario.cs / TrainStationDockingScenario.cs**: 上記テストで使用するテスト環境・シナリオ構築ユーティリティを提供し、列車・駅セットアップを簡潔化するサポートコード。  
- **RailGraphNetworkTestHelper.cs**: `RailComponent` 集合からノード/エッジ構造をスナップショット化し、ロード後の `RailGraph` との完全一致を比較するユーティリティ。  
- **SaveLoadJsonTestHelper.cs**: セーブ→破壊→ロードを一括実行する `SaveCorruptAndLoad` や `RemoveTrainUnitDockedAt` など、破損セーブ検証用のユーティリティ。  

## カバレッジ詳細メモ

- **ユニット層**: `SimpleTrainTest*` および `TrainUnitReverseTest` により `RailGraph` / `RailPosition` 関連アルゴリズムや `TrainUnit` の基本ロジックを網羅している。また、`TrainUnitAddCarTest` により列車への車両増結処理が正しく行われることも検証されている。  
- **統合層**: ドッキング／積み下ろし (`TrainStationDockingItemTransferTest.cs`) に加え、破損セーブや複列車復元を扱う `TrainStationDockingPersistenceTest.cs`、レールグラフ保存を扱う `TrainRailGraphSaveLoadConsistencyTest.cs`、貨物ループを扱う `TrainBidirectionalCargoLoopSaveDataTest.cs` が揃い、駅占有の安全性や `WaitForTicks` 復元まで自動検証できるようになった。  
- **シナリオ層**: `SimpleTrainTestUpdateTrain.cs` と `TrainSingleTwoStationIntegrationTest.cs` が単列車シナリオを、`TrainStationDockingConcurrencyTest.cs` が前後列車の競合パターンを、`TrainBidirectionalCargoLoopSaveDataTest.cs` が逆方向の貨物循環を担保する。長時間耐久や大規模路線でのマルチトレイン競合は依然未カバー。  

> 📌 **ギャップまとめ**: 「複数列車が同一路線を共有する長時間運行時の路線編集や駅状態変化を伴う再探索」「走行中の `TrainUnit.Reverse()` 呼び出し」「運行中の車両増結/切り離しによる編成変化」などは未カバーで、今後の優先課題として残る。

## 優先度1: 多列車シナリオの統合テスト強化

- **目的**: デッドロックの未検出を防ぎ、複数列車運用の基礎品質を保証する。  
- **理由**:  
  - 実プレイで最頻出のケースで、異常時の影響 (衝突・詰まり) が致命的。  
  - 星形／格子など高分岐ネットワークでの長時間シミュレーションと指標アサートは、後続シナリオ全般の信頼性を底上げする。  
- **必要テストと実装状況**:  
  - [ ] 交換・待避・復帰の一連挙動を網羅する多列車シナリオ (未実装)  
  - [x] 星形／格子ネットワークでの長時間運転検証 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainHugeAutoRunSaveLoadConsistencyTest.cs`)  
  - [x] 先詰まり・後続待機シナリオ (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStationDockingConcurrencyTest.cs`) — 先頭列車が駅を占有した状態で後続が安全に待機するかを検証  
  - [x] ループ構造での超長編成ドッキング検証 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStationDockingConcurrencyTest.cs`)  
  - [x] 双方向貨物ループシナリオ (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainBidirectionalCargoLoopSaveDataTest.cs`) — 2 編成が逆向きに走行し、荷物循環と SaveData を検証  
  - [x] 単列車×二駅の往復テスト (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainSingleTwoStationIntegrationTest.cs`) — シナリオテストとして単列車の往復を自動検証  
  - [x] 単駅での積み込み／荷降ろし切替確認 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainStationDockingItemTransferTest.cs`)  
- **着手ポイント**:  
  - 交換・待避・復帰の一連挙動を網羅するシナリオを `TrainTestHelper` / `TrainStationDockingScenario` ベースで構築。  
  - 走行中に `TrainUnit.Reverse()` を呼び出すケースや多両編成での反転など運行中の未検証パターンに加え、編成の増解結ケースも洗い出す。  

## 優先度2: セーブ/ロードの実ゲーム相当検証

- **目的**: セーブ/ロード後の状態再現性を保証し、長時間プレイの信頼性を確保する。  
- **理由**:  
  - プレイヤーが日常的に行う操作で、破綻すると進行不能バグにつながる。  
  - 走行・減速・駅ドックなど複数状態の再現は、時刻管理・予約・ドッキングの回帰も検知できる。  
  - 偏差許容を定義すれば自動テストが安定化し、将来のリグレッションを早期捕捉できる。  
- **必要テストと実装状況**:  
  - [x] 走行中・減速中・駅ドック状態を含む複数列車のセーブ/ロード再現 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs`)  
    - 位置・速度・残距離・自動運転状態・`WaitForTicks` 残量をスナップショット比較し、ロード後に完全一致することを検証。  
  - [x] `TrainUnit` dock 状態の堅牢性検証 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs`)  
    - 列車破棄時の駅占有解除、およびドッキング先ブロック欠損時の安全な Undock を自動テストで確認。  
  - [x] 貨物列車インベントリとダイアグラム設定の永続化 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs`)  
    - 貨物スロットの積載数とダイアグラム進行状態 (現在ノード・`WaitForTicks` 残量) がロード後も一致することを検証。  
  - [x] 破損セーブデータに対するフォールバック動作 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs`)  
    - `SaveLoadJsonTestHelper.SaveCorruptAndLoad` を用い、`DockingBlockPosition` 欠損時に自動的に Undock して再接続可能なことを確認。  
  - [x] 駅・レールブロックの保存/復元とインベントリ保持 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainRailGraphSaveLoadConsistencyTest.cs`)  
  - [x] 双方向貨物ループの SaveData 検証 (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainBidirectionalCargoLoopSaveDataTest.cs`) — 2 編成が逆方向に走るシナリオで SaveData に車両数・向きが保存されることを検証。  
- **着手ポイント**:  
  - `TrainAutoRunTestScenario` / `TrainStationDockingScenario` を拡張し、曲線やポイント切替を含む路線でのセーブ/ロードケースを量産できるようにする。  
  - `SaveLoadJsonTestHelper` の破壊ユーティリティを拡張し、ノード接続欠損やダイアグラム参照欠損など多様なフェイルインジェクションをテンプレ化する。  
  - SaveData 内での `TrainUnit` の向きやダイアグラム発車条件が正しく復元されているかを検証する追加テストを設計する。  

## Docking／TrainUnit 解体周りの個別課題

- **目的**: セーブ/ロードに絡む片方向参照や手動削除を行っても、駅・列車双方の状態が破綻しないことを保証する。  
- **主なテスト候補**:  
  - [ ] `StationDockingService` がロード時に `TrainUnit` の不整合を検知・解放することを検証するユニットテスト (未実装)。  
  - [x] `TrainUnit.Dispose` / `Destroy` が呼ばれたときに駅占有が確実に解除されることを確認するテスト (`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/TrainStationDockingPersistenceTest.cs`)。
