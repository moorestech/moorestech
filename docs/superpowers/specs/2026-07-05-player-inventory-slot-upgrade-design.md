# プレイヤーインベントリ スロット増量アップグレード 設計

日付: 2026-07-05
ステータス: 承認済み

## 概要

プレイヤーインベントリの所持可能アイテムスロット数を、研究ツリー経由で段階的に増やせるようにする。
初期スロット数を現状（9列×5行=45）の半分程度に減らし、研究の進行に応じて拡張していく。

Allow the player inventory slot count to be expanded in stages via the research tree.
The initial slot count is reduced to roughly half of the current 9×5=45 and grows as research progresses.

## 決定事項

- **獲得手段**: 研究ツリーの `clearedActions`（gameAction）経由
- **モデル**: 加算式ではなく「スロット数レベル」を置き、「レベルN解放」の冪等操作にする
- **レベル定義**: items.yml のルートに新配列を追加してレベル→スロット数を定義
- **初期値**: レベル0のスロット数を初期値とする（現状の半分程度、例: 18〜27）
- **状態の単位**: グローバル（ワールド共通）。研究・アンロック状態と同じ粒度
- **ホットバー**: 現状維持（常に「最後の行」の9スロット）。将来廃止予定のため差分最小を優先
- **未解放スロットUI**: 解放済みスロットだけ表示（ロック表示はしない）
- **サーバー実装方式**: `OpenableInventoryItemDataStoreService` に拡張メソッドを追加する方式（案A）

## 1. マスタデータ

### items.yml

ルートに3つ目の配列 `playerInventorySlotLevels` を追加する。

```yaml
- key: playerInventorySlotLevels
  type: array
  items:
    type: object
    properties:
    - key: slotCount
      type: integer
```

- **レベル = 配列インデックス**（0始まり）。レベル0が初期スロット数
- 明示的な `level` フィールドを持たせないことで、重複・歯抜けが構造的に発生しない
- スロット数は9の倍数を想定（データ運用ルール。スキーマでは強制しない）

### ref/gameAction.yml

enum `gameActionType` に `unlockPlayerInventorySlotLevel` を追加。パラメータは `{ level: integer }` のみ。
研究ノードの `clearedActions` から発火する。

## 2. サーバー状態管理

### 新規store: PlayerInventorySlotLevelDataStore（Game.PlayerInventory）

グローバル（ワールド共通）の単一レベル値を保持する。

- `int CurrentLevel`
- `int CurrentSlotCount` — マスタ（`playerInventorySlotLevels[CurrentLevel].slotCount`）から引く
- `void UnlockLevel(int level)` — `CurrentLevel = Max(CurrentLevel, level)` の**冪等操作**。低いレベル・同一レベルの再発火は無視される
- `IObservable<int> OnSlotCountChanged` — 実際にスロット数が増えたときのみ発火

### GameActionExecutor

`unlockPlayerInventorySlotLevel` の case を追加し、`PlayerInventorySlotLevelDataStore.UnlockLevel(param.Level)` を呼ぶだけ。

### OpenableInventoryItemDataStoreService（Core.Inventory）

`ExpandSlots(int newSize)` を追加。末尾に空スロットを追加するのみで、縮小は非対応（要求されたら無視 or 例外ではなく無視）。
既存アイテム・スロットインデックスは一切動かない。

### PlayerInventoryDataStore

- `OnSlotCountChanged` を購読し、全プレイヤーの `MainOpenableInventory` を `ExpandSlots` で拡張
- 新規プレイヤーのインベントリは現在レベルのスロット数で生成
- `PlayerInventoryConst.MainInventorySize` への依存を排除

### セーブ / ロード

- `WorldSaveAllInfoV1` に `inventorySlotLevel`（int）を1フィールド追加
- 永続化するのは**レベル値のみ**。スロット数はマスタ定義値のためセーブせず、ロード時に `playerInventorySlotLevels` から導出する
- レベルは配列インデックスと同義の順序値であり、マスタの採番に依存する揮発intではない（レベル定義の途中挿入はデータ運用で禁止）
- ロード時、セーブ済みレベルがマスタ定義範囲外（`>= playerInventorySlotLevels.Count`）なら最大レベルにクランプする
- **ロード順**: スロットレベル → プレイヤーインベントリの順を保証する
- ロード時、セーブ済みアイテム数が現レベルのスロット数を超える場合はアイテム数までインベントリを拡張する（アイテム消失防止の安全弁。旧45スロットセーブ対策を兼ねる）

## 3. プロトコル / 同期

- `PlayerInventoryResponseProtocol` のメインインベントリのループ上限を `PlayerInventoryConst.MainInventorySize` → `GetSlotSize()` に変更
- クライアントは **レスポンスの Main 配列長からスロット数を知る**。プロトコルへの新フィールド追加は不要
- **プレイ中のレベルアップ通知**: 専用パケットは作らない。サーバーは拡張時に新スロット分の通常更新イベント（`MainInventoryUpdateEvent`、空アイテム）を既存経路で発火する。クライアントは現在のリスト範囲外のスロット番号を受信したらリストを拡張し、UIを再構築する

## 4. クライアントUI

- `PlayerInventoryViewController` の45個プレハブ静的配置（`mainInventorySlotObjects`）を廃止し、**動的生成**に切り替える
  - `FilterSplitterBlockInventoryView.BuildColumns` と同パターン（GridLayoutGroup コンテナ + ItemSlotView プレハブの Instantiate）
  - プレハブ改修は uloop execute-dynamic-code 経由（Unity Editor 経由の正規ルート）で実施
- `LocalPlayerInventory` の固定45初期化を廃止し、サーバーレスポンス由来のサイズで構築。スロット数増加時は末尾に追加
- `HotBarView` / `PlayerInventoryViewController.DirectMove` 等の行数計算は「現在のスロット数の最後の9個」を動的に算出
- `PlayerInventoryConst.MainInventorySize` は廃止方向で参照箇所を動的値に置換。`MainInventoryColumns = 9` は列数として存続。`MainInventoryRows` は廃止（行数 = スロット数 / 9 で導出）

## 5. エラーハンドリング

- `UnlockLevel` にマスタ定義範囲外のレベル（`>= playerInventorySlotLevels.Count`）が来た場合は最大レベルにクランプ
- マスタに `playerInventorySlotLevels` が未定義（空）の場合は従来の45スロット固定で動作（マスタ移行期間の暫定挙動）
- クライアントが範囲外スロットの更新イベントを受信した場合、そのスロット番号+1 までリストを拡張してから適用する

## 6. テスト

### サーバー単体テスト

- gameAction 発火でレベルが上がり `CurrentSlotCount` が反映される
- 冪等性: 同一レベル・下位レベルの再発火で状態が変わらない、イベントも発火しない
- 拡張後、既存スロットのアイテムがインデックスごと保持される
- セーブ → ロードのラウンドトリップでレベルとインベントリ内容が復元される
- 旧形式セーブ（アイテム数 > レベル由来スロット数）でアイテムが消失しない
- 研究完了 → clearedActions → スロット数増加の統合テスト

### クライアント

- コンパイル確認（`uloop compile`）
- 動的生成後のスロット表示数・ホットバー位置（最終行）の動作確認

## 対象外（YAGNI）

- スロット数の縮小
- プレイヤー個別のレベル管理
- 未解放スロットのロック表示UI
- 専用の同期パケット
