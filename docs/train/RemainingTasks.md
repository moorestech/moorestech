# 鉄道システム 残タスク一覧

最終更新日: 2025-11-03

## 概要

このドキュメントは、鉄道システムのサーバー側で既に実装されている機能のうち、クライアント側で不足している実装タスクをまとめたものです。

---

## サーバー側で実装済みの主要機能（参考）

### 列車の基本機能
- ✅ TrainUnit: 列車ユニットの管理、移動、速度制御
- ✅ TrainCar: 車両の管理（インベントリスロット、燃料スロット、牽引力、重量、向き）
- ✅ RailPosition: レール上の位置管理
- ✅ TrainEntity: エンティティとしての列車（ワールド座標での表現）

### 速度・運転制御
- ✅ masconLevel: マスコンレベルによる加速度制御（-16777216 〜 +16777216）
- ✅ 物理シミュレーション: 摩擦、空気抵抗、牽引力の計算
- ✅ 自動減速: 目的地までの距離に応じた速度制御
- ✅ Tickベースの決定論的シミュレーション: 120Hz（1/120秒）での更新

### 自動運転システム
- ✅ TrainDiagram: 列車の運行ダイアグラム
- ✅ DiagramEntry: 停車駅と発車条件の管理
- ✅ 発車条件の種類: TrainInventoryFull, TrainInventoryEmpty, WaitForTicks

### 駅・ドッキング機能
- ✅ TrainUnitStationDocking: 駅へのドッキング処理
- ✅ TrainStationComponent: 旅客駅ブロック
- ✅ CargoplatformComponent: 貨物プラットフォーム
- ✅ 自動積み込み・積み下ろし: ドッキング中のアイテム転送

### レールシステム
- ✅ RailGraphDatastore: レールグラフの管理
- ✅ RailNode / RailComponent: レール接続の管理
- ✅ Front/Back ノードモデル: 有向グラフとしてのレール接続
- ✅ ベジェ曲線: レール間の距離計算
- ✅ 固定小数点距離: セーブ/ロードの再現性保証

### セーブ/ロード
- ✅ 列車位置、速度、ダイアグラム状態の保存と復元
- ✅ ドッキング状態の復元

---

## クライアント側で実装済みの機能（参考）

### ビジュアル化
- ✅ TrainEntityObject: 列車エンティティの表示と位置補間
- ✅ TrainRailObjectManager: レール接続のビジュアル化
- ✅ RailSplineComponent: Splineを使用したレール曲線の描画

### 配置システム
- ✅ TrainCarPlaceSystem: 列車をレールに配置する機能
- ✅ TrainCarPlacementDetector: 配置可能位置の検出
- ✅ TrainCarPreviewController: 配置プレビュー表示

### レール接続システム
- ✅ TrainRailConnectSystem: レール接続UI
- ✅ RailConnectPreviewObject: 接続プレビュー表示

---

## 残タスク: クライアント側の実装

### 【優先度：高】必須機能

#### 1. 列車インベントリUI
**概要**: 列車の貨物インベントリを開いてアイテムを操作する機能

**実装内容**:
- [ ] 列車のインベントリを開くUIステート（`TrainInventoryState`）
- [ ] 列車インベントリビュー（`TrainInventoryView`）の作成
  - 既存の`BlockInventoryState`パターンを参考に実装
  - `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/BlockInventoryState.cs` 参照
- [ ] 各車両のインベントリスロット表示
- [ ] 燃料スロットの表示と管理UI
- [ ] プレイヤーインベントリとのアイテム移動

**参考ファイル**:
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/BlockInventoryState.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/ChestBlockInventoryView.cs`

---

#### 2. 列車状態表示UI
**概要**: 列車の現在の状態をプレイヤーに表示する

**実装内容**:
- [ ] 現在速度の表示（数値またはメーター）
- [ ] masconレベルの表示（現在の加速度レベル）
- [ ] 自動運転/手動運転の状態表示
- [ ] ドッキング状態の表示（駅に停車中かどうか）
- [ ] HUD上での常時表示またはモーダル表示

**表示場所の候補**:
- 列車に乗車中（または選択中）のHUD
- 列車インベントリUI内の情報パネル

---

#### 3. 列車操作UI
**概要**: プレイヤーが列車を手動で操作するためのUI

**実装内容**:
- [ ] 手動運転時のmasconレベル操作UI
  - スライダーまたは+/-ボタンでレベル調整
  - 範囲: -16777216 〜 +16777216
  - キーボード操作（W/S）との連携
- [ ] 自動運転のON/OFF切り替えボタン
- [ ] 列車の向きを反転させるボタン
- [ ] 操作パネルの表示/非表示切り替え

**UI配置**:
- HUD上の操作パネル
- または専用のモーダルウィンドウ

---

### 【優先度：中】自動運転機能

#### 4. TrainDiagram（ダイアグラム）設定UI
**概要**: 列車の自動運転ルートと停車条件を設定する

**実装内容**:
- [ ] ダイアグラム設定ウィンドウ（`TrainDiagramView`）
- [ ] エントリリストの表示
  - 各エントリの停車駅（RailNode）の表示
  - エントリの順序番号表示
  - 現在のエントリのハイライト
- [ ] エントリの追加・削除ボタン
- [ ] 停車駅の選択UI
  - レールノード（駅）の選択方法の検討
  - マップ上での駅クリック選択
  - または駅リストからの選択
- [ ] 発車条件の設定UI
  - ラジオボタンまたはドロップダウンで条件タイプ選択
  - `TrainInventoryFull`: インベントリが満杯になったら出発
  - `TrainInventoryEmpty`: インベントリが空になったら出発
  - `WaitForTicks`: 指定Tick数待機
    - Tick数入力フィールド（数値入力）
- [ ] エントリの順序変更UI
  - ドラッグ&ドロップまたは上下移動ボタン
- [ ] 現在のダイアグラム状態の可視化
  - 次の目的地の表示
  - 発車条件の達成状況表示

**参考**:
- サーバー側: `moorestech_server/Assets/Scripts/Game.Train/Train/TrainDiagram.cs`
- 類似UI: チャレンジシステムのツリービュー

---

#### 5. 駅ブロックUI
**概要**: 駅ブロックの設定とインベントリ管理

**実装内容**:
- [ ] 駅ブロックのインベントリUI
  - 既存のブロックインベントリパターンを使用
  - `TrainStationBlockInventoryView`の作成
  - `CargoplatformBlockInventoryView`の作成
- [ ] 駅の状態表示
  - 現在ドッキング中の列車情報
  - 列車が到着予定かどうか（ダイアグラムに登録されているか）
- [ ] アイテム自動転送の設定UI（オプション）

**参考ファイル**:
- サーバー側: `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/StationComponent.cs`
- サーバー側: `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/CargoplatformComponent.cs`

---

### 【優先度：低】拡張機能

#### 6. 列車情報の詳細表示
**概要**: 列車の詳細情報を確認するUI

**実装内容**:
- [ ] 列車の編成情報
  - 車両数
  - 総重量
  - 総牽引力
  - 各車両の個別情報
- [ ] 列車IDの表示
- [ ] マスターデータの確認UI
  - addressablePath
  - 各種パラメータ

---

#### 7. デバッグ・開発支援UI
**概要**: 開発者向けのデバッグ情報表示

**実装内容**:
- [ ] レールグラフの可視化
  - JSON出力の表示
  - `RailGraphDatastore.CreateSnapshot()`の結果表示
- [ ] 列車の内部状態デバッグ表示
  - _remainingDistance
  - _accumulatedDistance
  - 現在のRailPosition詳細
- [ ] 速度・加速度のグラフ表示
  - リアルタイムグラフ
  - 履歴表示

**参考**:
- サーバー側: `moorestech_server/Assets/Scripts/Game.Train/RailGraph/RailGraphDatastore.cs`

---

## 残タスク: サーバー側のプロトコル実装

クライアント側のUIを機能させるために、以下のサーバー側プロトコルが必要です。

### 【必須】列車制御プロトコル

#### 1. TrainDiagram制御プロトコル
- [ ] **AddDiagramEntryProtocol**: ダイアグラムエントリの追加
  - リクエスト: TrainId, RailNodeId
  - レスポンス: 成功/失敗
- [ ] **RemoveDiagramEntryProtocol**: エントリの削除
  - リクエスト: TrainId, EntryIndex
  - レスポンス: 成功/失敗
- [ ] **InsertDiagramEntryProtocol**: 指定位置にエントリを挿入
  - リクエスト: TrainId, Index, RailNodeId
  - レスポンス: 成功/失敗
- [ ] **SetDepartureConditionProtocol**: 発車条件の設定
  - リクエスト: TrainId, EntryIndex, ConditionType[], WaitTicks?
  - レスポンス: 成功/失敗
- [ ] **MoveDiagramIndexProtocol**: 現在のダイアグラムインデックスを変更
  - リクエスト: TrainId, NewIndex
  - レスポンス: 成功/失敗

**参考**:
- `moorestech_server/Assets/Scripts/Game.Train/Train/TrainDiagram.cs`
- 実装パターン: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/`

---

#### 2. 列車操作プロトコル
- [ ] **SetTrainMasconLevelProtocol**: masconレベルの設定
  - リクエスト: TrainId, MasconLevel (int, -16777216 〜 +16777216)
  - レスポンス: 成功/失敗
- [ ] **ToggleAutoRunProtocol**: 自動運転のON/OFF
  - リクエスト: TrainId, IsAutoRun (bool)
  - レスポンス: 成功/失敗
- [ ] **ReverseTrainProtocol**: 列車の向きを反転
  - リクエスト: TrainId
  - レスポンス: 成功/失敗

**参考**:
- `moorestech_server/Assets/Scripts/Game.Train/Train/TrainUnit.cs`
  - `masconLevel`プロパティ
  - `TurnOnAutoRun()` / `TurnOffAutoRun()`メソッド
  - `Reverse()`メソッド

---

#### 3. 列車状態取得プロトコル
- [ ] **GetTrainStatusProtocol**: 列車の状態を取得
  - リクエスト: TrainId
  - レスポンス:
    - CurrentSpeed (double)
    - MasconLevel (int)
    - IsAutoRun (bool)
    - IsDocked (bool)
    - RailPosition情報
    - TrainDiagram情報（現在のインデックス、エントリリスト）
- [ ] **GetTrainInventoryProtocol**: 列車のインベントリ情報を取得
  - リクエスト: TrainId
  - レスポンス: 各車両のインベントリ情報

**参考**:
- `moorestech_server/Assets/Scripts/Game.Train/Train/TrainUnit.cs`
- 既存のGetプロトコル: `GetBlockInventoryProtocol.cs`など

---

#### 4. 列車インベントリ操作プロトコル
- [ ] **InsertTrainInventoryProtocol**: 列車インベントリにアイテムを入れる
  - リクエスト: TrainId, CarIndex, SlotIndex, ItemId, Count
  - レスポンス: 成功/失敗
- [ ] **GrabTrainInventoryProtocol**: 列車インベントリからアイテムを取り出す
  - リクエスト: TrainId, CarIndex, SlotIndex
  - レスポンス: 成功/失敗
- [ ] **GetTrainCarInventoryProtocol**: 特定車両のインベントリを取得
  - リクエスト: TrainId, CarIndex
  - レスポンス: インベントリ情報

**実装方針**:
- 既存のインベントリプロトコルを参考に実装
- `IOpenableInventory`インターフェースを活用できるか検討
- 車両インデックスでの指定方法

**参考**:
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/InsertItemProtocol.cs`
- `moorestech_server/Assets/Scripts/Game.Train/Train/TrainCar.cs`

---

### 【推奨】イベントプロトコル

#### 5. 列車状態変更イベント
- [ ] **TrainStatusChangedEvent**: 列車の状態変更を通知
  - 速度変更
  - masconLevel変更
  - 自動運転状態変更
  - ドッキング状態変更
- [ ] **TrainDiagramChangedEvent**: ダイアグラム変更を通知
  - エントリの追加/削除
  - 現在のインデックス変更
  - 発車条件変更
- [ ] **TrainInventoryChangedEvent**: 列車インベントリ変更を通知
  - アイテム追加/削除時

**実装方針**:
- 既存のイベントシステムを活用
- `Server.Event.EventReceive`パッケージを参考

**参考**:
- `moorestech_server/Assets/Scripts/Server.Event/EventReceive/`
- 既存のEntityイベントで部分的に対応済み（位置情報）

---

## 実装の推奨順序

### フェーズ1: 基本的な列車操作
**目標**: プレイヤーが列車を手動で操作できるようにする

1. サーバー側プロトコル実装
   - [ ] SetTrainMasconLevelProtocol
   - [ ] ToggleAutoRunProtocol
   - [ ] ReverseTrainProtocol
   - [ ] GetTrainStatusProtocol

2. クライアント側UI実装
   - [ ] 列車操作UI（masconレベル操作、自動運転切り替え、反転ボタン）
   - [ ] 列車状態表示UI（速度、masconレベル、状態表示）

3. テスト
   - [ ] 手動操作のテスト
   - [ ] UIの動作確認

---

### フェーズ2: インベントリ管理
**目標**: 列車のインベントリを操作できるようにする

1. サーバー側プロトコル実装
   - [ ] GetTrainCarInventoryProtocol
   - [ ] InsertTrainInventoryProtocol
   - [ ] GrabTrainInventoryProtocol
   - [ ] TrainInventoryChangedEvent（推奨）

2. クライアント側UI実装
   - [ ] TrainInventoryState
   - [ ] TrainInventoryView
   - [ ] 車両インベントリスロット表示
   - [ ] 燃料スロット表示

3. テスト
   - [ ] インベントリ操作のテスト
   - [ ] アイテム転送のテスト

---

### フェーズ3: 自動運転
**目標**: ダイアグラムを設定して列車を自動運転できるようにする

1. サーバー側プロトコル実装
   - [ ] AddDiagramEntryProtocol
   - [ ] RemoveDiagramEntryProtocol
   - [ ] InsertDiagramEntryProtocol
   - [ ] SetDepartureConditionProtocol
   - [ ] MoveDiagramIndexProtocol
   - [ ] TrainDiagramChangedEvent（推奨）

2. クライアント側UI実装
   - [ ] TrainDiagramView
   - [ ] エントリリスト表示
   - [ ] 停車駅選択UI
   - [ ] 発車条件設定UI
   - [ ] エントリ順序変更UI

3. テスト
   - [ ] ダイアグラム設定のテスト
   - [ ] 自動運転動作のテスト
   - [ ] 発車条件の動作確認

---

### フェーズ4: 駅機能の拡充
**目標**: 駅ブロックのUIを整備する

1. クライアント側UI実装
   - [ ] TrainStationBlockInventoryView
   - [ ] CargoplatformBlockInventoryView
   - [ ] 駅の状態表示

2. テスト
   - [ ] 駅インベントリの動作確認
   - [ ] 自動積み込み・積み下ろしのテスト

---

### フェーズ5: 詳細情報とデバッグ
**目標**: 詳細情報表示とデバッグ機能を追加

1. クライアント側UI実装
   - [ ] 列車編成情報表示
   - [ ] デバッグ情報表示UI
   - [ ] レールグラフ可視化

2. テスト
   - [ ] 開発者向け機能の動作確認

---

## 参考資料

### ドキュメント
- `docs/train/README.md`: 鉄道システムドキュメントインデックス
- `docs/train/TrainSystemNotes.md`: 実装時の運用ノート
- `docs/train/TrainTickSimulation.md`: Tickシミュレーション仕様
- `docs/ServerGuide.md`: サーバー実装ガイド
- `docs/ClientGuide.md`: クライアント実装ガイド
- `docs/ProtocolImplementationGuide.md`: プロトコル実装ガイド

### マスターデータ
- `VanillaSchema/train.yml`: 列車ユニットのスキーマ定義

### テスト
- `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs`: テスト用ブロックID定義
- `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/`: テスト用マスターデータ

### 既存UIパターン参考
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/BlockInventoryState.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/PlayerInventoryState.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/ChestBlockInventoryView.cs`

### プロトコル実装参考
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceTrainCarOnRailProtocol.cs`
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/InsertItemProtocol.cs`
- `moorestech_server/Assets/Scripts/Server.Event/EventReceive/`

---

## 注意事項

### 既存システムとの整合性
- インベントリ操作は既存の`IOpenableInventory`パターンに準拠
- UIステート管理は`UIStateEnum`と`IUIState`を使用
- プロトコルは`IPacketResponse`を実装し、`ProtocolMessagePackBase`を継承

### 命名規則
- プロトコルタグ: `va:trainXxxx`形式（例: `va:trainSetMascon`）
- UIクラス: `TrainXxxxxView`または`TrainXxxxxState`
- プロトコルクラス: `XxxxxTrainXxxxxProtocol`

### テスト
- 各フェーズで必ずユニットテストを実装
- 既存のテストパターンに従う
- テスト用マスターデータは`Tests.Module.TestMod.ForUnitTest`を使用

### パフォーマンス
- 列車状態の更新頻度に注意（120Hz tickに同期）
- UIの更新は適度な頻度に抑える（毎フレームは避ける）
- イベントベースの更新を活用

---

## 進捗管理

このドキュメントは実装の進捗に応じて更新してください。

- タスク完了時はチェックボックスにチェックを入れる
- 新たに判明したタスクは適切なセクションに追加
- 実装方針が変更された場合は該当箇所を更新
