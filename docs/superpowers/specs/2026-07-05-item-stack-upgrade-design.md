# アイテムスタック数アップグレード機能 設計書

日付: 2026-07-05
ブランチ: feature/item-stack-upgrade

## 概要

研究（research）の完了によって、アイテムごとのスタック上限を段階的に引き上げる機能。
アイテム固有の `maxStack` は廃止し、全アイテムが「レベルテーブル」を参照する方式に置き換える。
スタック上限の増加はプレイヤーインベントリ・チェスト・機械スロット等、すべてのインベントリに適用される。

## 要件（壁打ちで確定した決定）

| 項目 | 決定 |
|---|---|
| 獲得方法 | 研究の clearedActions（GameAction）で解放 |
| 適用粒度 | アイテム個別 × 段階式（レベル） |
| 適用範囲 | 全インベントリ（プレイヤー・チェスト・機械等すべて） |
| テーブル定義 | items.yml の `data` と同階層にグローバル共有で定義。アイテムはどのテーブルを使うか参照 |
| maxStack | 廃止。全アイテムがテーブル必須参照に移行 |
| アクション | 冪等。「特定レベルを解放する」（increment ではない） |
| 動的状態の所有 | Core.Item（マスターは静的テーブル定義のみ） |
| クライアント同期 | 専用通信なし。同期済みの研究完了状態からレベルを導出 |
| UI | 既存UIへの反映のみ（専用表示は作らない） |

## 1. マスタデータ（スキーマ）

編集時は edit-schema スキルを参照する。

### items.yml

- `data` と同階層に `itemStackLevelTables` を新設
  - 各テーブル = `{ guid, name, stackCounts: [整数, ...] }`
  - 配列インデックス+1 がレベル（レベル1 = `stackCounts[0]`）
- 各アイテムの `maxStack` を**削除**し、必須の `stackLevelTableGuid`（foreignKey → itemStackLevelTables）を追加
- 固定スタック数のアイテム（例: 常に10個）は「stackCounts が1要素だけのテーブル」で表現する

### gameAction.yml

- enum に `unlockItemStackLevel` を追加
  - パラメータ: `targetItemGuids`（アイテムGUID配列）、`level`（解放するレベル）
  - **冪等**: 実効レベル = これまでに解放された最大レベル。内部的には `max(現在, 指定)` で保持。
    何度実行しても、どの順で実行しても結果が同じため、既存 unlock 系アクションと同格に扱える

## 2. サーバー実行時アーキテクチャ

### 責務分離

- **ItemMaster（Core.Master）**: 静的情報のみ。テーブル定義の lookup と `ItemMasterElement.StackLevelTableGuid` を提供。可変状態は持たない
- **Core.Item に新設: `ItemStackLevelDataStore`**（名前は実装時に確定）
  - アイテムGUID → 解放済み最大レベルの辞書（初期値レベル1）
  - `GetMaxStack(ItemId)`: マスタのテーブルを引いて `stackCounts[解放レベル-1]` を返す
  - `UnlockStackLevel(ItemGuid, level)`: `max()` で更新。変化時に UniRx（Subject）で通知
  - レベルはテーブル長でクランプ（最大レベル超の解放指定は最大レベル扱い）
  - アクセス経路: `ItemStack` 内部からは `InternalItemContext` 経由（ItemStackFactory と同パターン）、
    外部からは ServerContext / クライアント側コンテキスト経由

### GameAction

- `GameActionExecutor.ExecuteAction` に `unlockItemStackLevel` 分岐を追加し、`ItemStackLevelDataStore` を更新
- 冪等なので `ExecuteUnlockActions`（ロード時の研究・チャレンジ再実行パス）にも含める（既存 unlock 系と同じ扱い）

### maxStack 参照箇所の移行

- スキーマから `maxStack` が消えると `ItemMasterElement.MaxStack` の全参照がコンパイルエラーになる
- 全箇所を `GetMaxStack(ItemId)` 経由に機械的に置換する（置換漏れはビルドが検出する）
- 主な参照箇所: `Core.Item/Implementation/ItemStack.cs`（コンストラクタ検証・AddItem・IsAllowedToAdd）、
  `Game.Train/Unit/Containers/ItemTrainCarContainer.cs`、`Game.Map/VanillaStaticMapObject.cs`、
  クライアント `PlayerInventoryViewController.cs` ほか

### 永続化とロード順（重要な設計判断）

- `WorldLoaderFromJson.Load()` は インベントリ復元(103行) が 研究ロード(107行) より**先**。
  研究再実行だけに頼ると、強化後の個数（例: 200個）を持つセーブが `ItemStack` コンストラクタの
  上限検証（ItemStack.cs:22-23）で例外死する
- 対策: 既存の GameUnlockState と同じ「冪等 unlock ＋ 派生状態も永続化して先頭でロード」パターンを踏襲
  - レベル辞書を独自セーブセクションとして保存
  - `Load()` 先頭（GameUnlockState と同じ位置、97行付近）で復元
  - その後の研究再実行は冪等なので二重適用しても無害

## 3. クライアント側

**新規プロトコル・イベント・ハンドシェイク拡張は作らない。**

- クライアントは楽観的ミラー: ドラッグ&ドロップ等は共有コード `ItemStack.AddItem`
  （`LocalPlayerInventoryController.cs:58`）でローカル予測するため、上限知識は共有ロジックに本質的に埋まっている
- レベルは**導出**する: クライアントは既に研究完了状態を同期している
  （初期取得 ＋ `ResearchCompleteEventPacket`）。完了済み研究の clearedActions から
  `unlockItemStackLevel` を読み取り、クライアント側の `ItemStackLevelDataStore` に適用する
  - 冪等（max適用）なので適用順は不問
  - 研究完了イベント受信時も同じ導出を1回走らせる
- **実装時の必須確認**: 初期ロードシーケンスで研究完了一覧の取得がインベントリパケット処理より
  先であること。先でなければロード中に取得を差し込む（先述のコンストラクタ検証で例外死するため）

## 4. 移行・バリデーション

### moorestech_master の items.json 移行

- 移行スクリプトを用意: 現行 `maxStack` の値の種類ごとにテーブルを自動生成
  （例: 値100 → `Stack100` テーブル、`stackCounts: [100]`）し、各アイテムに `stackLevelTableGuid` を
  付与、`maxStack` キーを削除。現行挙動を完全維持し、成長カーブの設計は後で mooreseditor 上で行う
- 実行前に mooreseditor が起動していないことを確認（起動中は外部編集が書き戻される）

### バリデーション（validate-schema スキルで確認）

- アイテムの `stackLevelTableGuid` が実在するテーブルを指すこと
- `unlockItemStackLevel` の `targetItemGuids` が実在アイテムを指すこと
- `unlockItemStackLevel` の `level` が対象アイテムのテーブル長以下であること
- テーブルの `stackCounts` が空でなく、全要素が1以上であること（単調増加は強制しない）

## 5. テスト

creating-server-tests スキルに従いサーバー側テストを追加（新規 .cs は Unity 再起動が必要な点に注意）。

1. **アクション単体**: `unlockItemStackLevel` 実行で `GetMaxStack` が変わる。
   冪等性（二重実行・低レベルの後追い実行で変化しない）。テーブル長超のクランプ
2. **インベントリ結合**: 基礎上限を超える個数がアップグレード後に挿入・合算できる。
   アップグレード前は従来通りあふれる
3. **セーブロード**: 強化後の個数（例: 200個）を持つセーブが正常にロードできる（ロード順の回帰）。
   レベル状態自体の保存・復元
4. **研究結合**: 研究完了 → レベル反映。セーブロード後の再実行でも二重適用されない
5. **クライアント導出**: 完了済み研究からのレベル導出が正しく行われる（テスト可能な範囲で）

## スコープ外

- 専用UI（現在レベル表示・ツールチップ等）
- 成長カーブの実データ設計（mooreseditor 上で後日実施）
- スタック上限の引き下げ（レベルは上がる一方の前提）
