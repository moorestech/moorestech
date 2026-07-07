# Satisfactory式設置システム 設計書

日付: 2026-07-03
ブランチ: feature/electric-wire-system からの派生を想定

## 1. 概要とゴール

設置システムを「ブロックをアイテムとして保持し、ホットバーから選択して設置する」現行モデルから、Satisfactory式の以下のモデルへ全面移行する。

- **ビルドメニューUI**から設置対象を選択する（インベントリ・ホットバーとは無関係）
- 設置時に**インベントリ全体から建設コスト（原材料リスト）を消費**する
- 破壊（解体）時は**建設コストが全額返却**される

これに伴い、**ブロックアイテム・車両アイテムという概念を廃止**する。設置に関わるすべての選択を「ビルドメニュー選択」に一本化し、保持アイテム駆動の設置システム選択（`PlaceSystemSelector` の `UsePlaceItems` マッチング）と `PlaceBlockFromHotBarProtocol` を**完全に削除**する（段階移行はしない）。

### アイテムとして残るもの / 消えるもの

- **残る**: 電線・歯車チェーン・レールなどの「敷設素材」。工場で生産する中間素材であり、接続ツールが距離に比例して消費する（Satisfactoryのケーブルと同じ位置づけ）。クラフトレシピも存続。
- **消える**: 全ブロックアイテム、列車車両アイテム。items.json から削除し、対応するクラフトレシピは建設コストへ変換して削除する。

## 2. ビルドメニューのエントリモデル

ビルドメニューには3種類のエントリが並ぶ。

| エントリ種別 | マスタソース | 設置システム | コストモデル |
|---|---|---|---|
| ブロック | blocks.json（通常ブロック、レール橋脚、電柱、歯車ポール含む） | CommonBlockPlaceSystem / TrainRailPlaceSystem | `requiredItems`（固定額） |
| 接続ツール | placeSystem.json（レール接続・電線接続・チェーン接続） | TrainRailConnectSystem / ElectricWireConnectSystem / GearChainPoleConnectSystem | 素材アイテムを距離比例で消費（既存の `railItems` / `electricWireItems` / `gearChainItems` を踏襲） |
| 列車車両 | train.json trainCars | TrainCarPlaceSystem | `requiredItems`（train.yml に新設） |

### 表示・解放ルール

- ブロック: `BlockUnlockStateInfo` で解放済みのもののみ表示
- 列車車両: `TrainCarUnlockStateInfo` で解放済みのもののみ表示
- 接続ツール: **常時表示**（素材アイテムを持っていなければ実行できないだけ）

## 3. マスタデータ（VanillaSchema）

### blocks.yml（トップレベル追加）

- `requiredItems: [{itemGuid, count}]` — 建設コスト。`electricWireItems` と同じ形式を踏襲
- `imagePath` — ビルドメニュー用アイコン（旧ブロックアイテムの imagePath を移植）
- `category` — ビルドメニューのタブ分類
- `sortPriority` — タブ内の並び順

### train.yml

- `trainCars` の各要素に `requiredItems` を追加
- `trainCars` の `itemGuid` を削除（車両アイテム廃止のため）

### placeSystem.yml（役割転換）

- `usePlaceItems`（保持アイテムによる発動定義）を廃止
- ビルドメニューの「接続ツールエントリ」定義に転換: `placeMode` / `name` / `imagePath` / `category` / `sortPriority`
- レール橋脚の TrainRailPlaceSystem 起動は「選択ブロックの `blockType == TrainRail`」で判定するため、placeSystem エントリは不要

### ref/gameAction.yml

- `unlockBlock`（`unlockBlockGuids` 配列）を追加
- `unlockTrainCar`（`unlockTrainCarGuids` 配列）を追加

### マスタJSON移行（moorestech_master v8）

移行スクリプトを用意する（mooreseditor 起動中はJSONが書き戻されるため、編集時は mooreseditor を終了しておくこと）。

1. ブロック・車両を結果とするクラフトレシピを `requiredItems` へ機械変換し、該当レシピを削除
2. items.json からブロックアイテム・車両アイテムを削除
3. blocks.json に `requiredItems` / `imagePath` / `category` / `sortPriority` を設定（imagePath は旧アイテムから移植）
4. research / challenges の `unlockCraftRecipe`（ブロック・車両レシピ対象）を `unlockBlock` / `unlockTrainCar` に置換
5. **レール素材アイテムの分離**: 現在レール橋脚ブロックとレール素材（train.json `railItems`）が同一 itemGuid を共用している。橋脚は「ブロック（requiredItems）」になるため、**「レール」素材アイテムを新規に作成**し、`railItems` の参照をそちらへ付け替える。レール素材のクラフトレシピも新設
6. 敷設素材（電線・チェーン・レール）のアイテムとレシピは存続

## 4. サーバー

### 新 PlaceBlockProtocol（PlaceBlockFromHotBarProtocol の置き換え）

- ペイロード: `PlayerId + BlockId + List<PlaceInfo>`（HotBarSlot は持たない）
- 処理フロー（電線の Evaluate / Execute 分離パターンを踏襲）:
  1. `BlockUnlockStateInfo` で解放済みかチェック（未解放なら全拒否）
  2. PlaceInfo を先頭から順に処理。セルごとに「建設コスト + 電線自動接続コスト（`ElectricWireAutoConnectService`）」を Evaluate
  3. 賄えるセルだけ `TryAddBlock` + 消費を実行する（**足りる分だけ設置**。不足セルはスキップ）
  4. 消費はメインインベントリ全スロットから順次減算
- レール橋脚の単体設置もこのプロトコルに乗る（`BlockCreateParams` で `RailBridgePierComponentStateDetail` を運ぶ既存構造のまま）
- `PlaceInfoMessagePack` / `BlockCreateParamMessagePack` は `RailConnectWithPlacePierProtocol` と `ElectricWireExtendProtocol` が型を再利用しているため、**共有DTOファイルへ移設**してから `PlaceBlockFromHotBarProtocol` を削除する

### スロット指定消費プロトコルの改修（4本）

| プロトコル | 変更 |
|---|---|
| `PlaceTrainCarOnRailProtocol` | `HotBarSlot` → `TrainCarGuid` 指定。車両の `requiredItems` をインベントリ横断で消費 |
| `AttachTrainCarToUnitProtocol` | 同上 |
| `RailConnectWithPlacePierProtocol` | `PierInventorySlot` → 橋脚 `BlockId` 指定。橋脚の `requiredItems` + レール素材の距離比例消費 |
| `ElectricWireExtendProtocol` | `PoleInventorySlot` → 電柱 `BlockId` 指定。電柱の `requiredItems` + 電線の距離比例消費 |

### 現行維持のプロトコル

- `RailConnectionEditProtocol` / `GearChainConnectionEditProtocol` / `ElectricWireConnectionEditProtocol`: 既にインベントリ検索型の素材消費であり目標モデルと整合済み。変更なし
- 素材が複数種類ある場合（電線Mk1/Mk2等）の選定は、`ElectricWireAutoConnectService` と同じ「マスタ定義順で所持しているものを採用」ルールに統一する（現在クライアントが保持アイテムIDを渡している箇所は、この規則でクライアント側が選定して送る）
- レール切断時の距離比例返却も現行維持

### 返却（解体）

- `RemoveBlockProtocol`: 「ブロックアイテム1個返却」を「`requiredItems` 全額返却」に差し替え。内部インベントリの中身返却と `IGetRefundItemsInfo`（電線返却等）は現行維持。プレイヤーインベントリに入り切らない場合は破壊拒否（現行維持）
- `RemoveTrainCarProtocol`: 「車両アイテム1個返却」を「車両の `requiredItems` 全額返却」に差し替え。コンテナ中身の返却は現行維持

### アンロック

- `Game.UnlockState` に `BlockUnlockStateInfo` / `TrainCarUnlockStateInfo` を新設（既存4種と同型）
- gameAction の `unlockBlock` / `unlockTrainCar` で解放。初期解放フラグにも対応
- 既存のアンロック状態同期の仕組みに乗せてクライアントへ配信

## 5. クライアント

### 選択モデルの刷新

- `PlaceSystemUpdateContext` から `HoldingItemId` / `CurrentSelectHotbarSlotIndex` / `PreviousSelectHotbarSlotIndex` を排除し、`PlacementSelection`（ブロック / 接続ツール / 車両のいずれかを表す選択状態）を保持する
- `PlaceSystemSelector` は `PlacementSelection` から設置システムを決定する:
  - ブロックかつ `blockType == TrainRail` → TrainRailPlaceSystem
  - ブロック（その他） → CommonBlockPlaceSystem
  - 接続ツール → placeMode に応じた各 Connect システム
  - 車両 → TrainCarPlaceSystem
- 既存の `HoldingItemId` 参照9箇所（CommonBlockPlaceSystem / TrainRailPlaceSystem / TrainCarPlaceSystem / TrainRailConnectSystem / GearChainPoleConnectSystem / ElectricWireExtendMode / PlaceSystemUtil ほか）を `PlacementSelection` 参照に置換
- `TrainRailConnectSystem` の橋脚インベントリ検索、`ElectricWireExtendRequestSender` の電柱スロット検索は不要になり、選択ブロックの GUID 送信に単純化

### ビルドメニューUI（新設）

- カテゴリタブ + グリッドで3種のエントリを統合表示
- ブロック / 車両エントリ: 建設コスト（素材アイコン + 必要数 / 所持数）を表示、不足素材は赤字
- 接続ツールエントリ: 消費素材と所持数を表示
- UIState 遷移: B → ビルドメニュー → エントリ選択 → 設置モード（`PlaceBlockState` 相当、選択コンテキスト付き）
- アンロック済みエントリのみ表示（接続ツールは常時表示）

### プレビュー

- 設置プレビューに必要素材と充足状況を表示（電線消費数表示の一般化）
- ドラッグ複数設置: 所持素材で賄える個数まで設置可、超過分は赤表示。`MarkInsufficientItemPreviewsAsNotPlaceable`（選択スロット1枠基準）をインベントリ横断の充足判定に差し替え

### ホットバー

- 設置との結合が完全になくなり、素材・道具用として存続。ビルドショートカット化（ピン留め）は今回のスコープ外
- 採掘系（MapObjectMining）等のホットバー参照は現状維持

### クラフトUI

- ブロック・車両レシピがマスタから消えるため表示は自然に減る。ブロックアイテム参照コードの掃除を行う

## 6. テスト

- サーバー
  - `PlaceBlockProtocolTest`（新設）: コスト消費 / 不足時の足りる分だけ設置 / アンロックゲート / 電線自動接続との合成消費 / 橋脚設置の BlockCreateParams
  - 改修4プロトコルの `requiredItems` 消費テスト（TrainCar設置×2、橋脚付きレール接続、電柱延長）
  - `RemoveBlockProtocol` の全額返却テスト更新、`RemoveTrainCarProtocol` の全額返却テスト
  - 既存 `PlaceHotBarBlockProtocolTest` は削除し新テストで置き換え
  - テスト用マスタ（ForUnitTest mod）にも `requiredItems` 等の追加が必要
- クライアント
  - プレビュー充足計算（インベントリ横断）のユニットテスト
  - `CommonBlockPlacePointCalculator` は変更なし（既存テスト維持）

## 7. 実装順序（概略）

1. スキーマ追加・変更（edit-schema スキル使用）→ SourceGenerator 実行
2. テスト用マスタ / 本番マスタの移行スクリプト作成・適用
3. サーバー: UnlockState 2種 → 共有DTO移設 → 新 PlaceBlockProtocol → 改修4プロトコル → 返却変更 → 旧プロトコル削除
4. クライアント: PlacementSelection 導入 → PlaceSystemSelector 改修 → 各設置システムの参照置換 → ビルドメニューUI → プレビュー
5. 統合テスト・実機確認

## 8. 決定事項の記録

- 大方針: ブロックアイテムを概念ごと廃止（案A）
- 移行スコープ: 全設置システムを一括移行。`PlaceBlockFromHotBarProtocol` は完全削除
- ドラッグ複数設置で素材不足時: 足りる分だけ設置
- ホットバー: 現状維持（ビルドショートカット化は後回し）
- アンロック: `unlockBlock` / `unlockTrainCar` 新設。接続ツールは常時表示
- 破壊返却: `requiredItems` 全額 + 内部インベントリ + `IGetRefundItemsInfo`。インベントリ満杯時は破壊拒否
- レール素材アイテム: 橋脚ブロックとのGUID共用を解消し、本番マスタに素材アイテムを新規作成
