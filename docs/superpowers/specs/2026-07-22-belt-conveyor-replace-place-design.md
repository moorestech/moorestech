# ベルトコンベア リプレース設置 設計spec

日付: 2026-07-22
ステータス: ユーザー承認済み（ブレスト対話にて範囲・搬送品引き継ぎ・操作モードを確定）

## 概要

設置ドラッグの起点セルに既存のベルト系ブロックがある場合、ドラッグパス上の既存ベルト系ブロックを
選択中ブロックで「置き換え設置（リプレース）」できるようにする。向きの引き直し・ティアアップグレード・
分岐器への差し替えを1ドラッグで行えるようにするのが目的。

## 決定事項（ユーザー確定）

1. **対象範囲**: 異種ベルトの置き換え、および分岐コンベア（FilterSplitter含む）への置き換えを含む
2. **搬送中アイテム**: 新ブロックへ引き継ぐ（進行率維持）。ロスト・地面ドロップはさせない
3. **操作モード**: 新モードは作らない。既存の設置モード（PlaceBlockState）内の挙動拡張。修飾キーなし

## リプレース可能ファミリー

blockType が **{BeltConveyor, GearBeltConveyor, FilterSplitter}** のブロック。相互に置き換え可能（対称）。
上り/下り/分岐器は同族の別ブロックIDであり、同一ルールで処理される。全て1x1のためセルマッピング問題はない。

## 発動条件と挙動マトリクス

- `PlaceBlockState` 中、手持ち（`BlockPlacementTarget`）がファミリーのブロックで、
  **左ドラッグの起点セルに既存ファミリーブロックがある**とき、そのドラッグ1回がリプレースドラッグになる
- 単クリックは「起点=終点のドラッグ」として同じ扱い（フィルター分岐器のクリック置換をカバー）
- パス上の各セルの扱い:
  - 空セル → 通常設置（コスト消費）
  - ファミリーブロックあり → リプレース
  - 非ファミリーブロックあり → スキップ（従来通り）
- 置き換え結果が**同一ブロックID・同一向き・同一傾斜になるセルはno-op**（送信しない。冪等）
- 起点が空セルの通常ドラッグは挙動不変（既存ブロックは全スキップ）
- UIステート追加・キー割り当て追加は一切なし。誤操作対策はプレビューのリプレース色で担保

## サーバー設計

### プロトコル

`PlaceBlockProtocol`（`va:placeBlock`）を拡張し、セル単位の `isReplace` フラグを追加する。
プロトコル新設はしない（1ドメイン1プロトコル・Mode分岐の標準に従う）。
クライアントが Remove→Place を2発送る案は途中失敗で穴が空くため不採用。サーバー側でアトミックに処理する。

### サーバー処理（1セルごと、isReplace=true の場合）

1. 対象座標のブロックが**ファミリーであることを検証**（違えばスキップ — 改造クライアントへの防御）
2. 旧ブロックの搬送中アイテムを退避
3. 旧ブロック撤去。`BlockRemoveReason` に `Replace` を追加（クライアント演出・購読側が区別できるように）
4. **常に「旧建設コスト返却 → 新コスト消費」の順で実行**。同型なら実質ゼロ、異種なら自然に差額精算。
   返却先インベントリ満杯で返却不能・新コスト不足の場合はそのセルは失敗（ロールバック不要な順序で処理）
5. 退避アイテムを新ブロックへ引き継ぎ

### 搬送中アイテムの引き継ぎ

- ベルト系→ベルト系: 各アイテムの**進行率（%）を維持**して新ブロックへ挿入（速度差があっても位置比率で引き継ぐ）
- 新ブロック側に収まらない分（スロット構造の差異等）は**プレイヤーインベントリへ返却**、
  それも満杯ならそのセルのリプレース自体を失敗させる（アイテムロスト・地面ドロップ禁止）

## クライアント設計

- `BeltConveyorPlacePointCalculator` の既存ブロック判定（`IsNotExistBlock`）を
  「ファミリーならPlaceable（リプレース）」に拡張し、`PlaceInfo` に IsReplace を追加
- FilterSplitter は `CommonBlockPlaceSystem` 経由（`PlaceSystemSelector` の分岐は既存のまま）のため、
  ファミリー判定・リプレース判定を**共通ユーティリティ**に置き両システムから使う
- リプレースドラッグの判定は「起点セルに既存ファミリーブロックがあるか」をドラッグ開始時に一度だけ確定
- プレビュー: リプレース対象セルは既存の設置可否色分けに**リプレース色を1色追加**して視覚的に区別
- コスト先読み（賄えるセル数計算）はリプレースセルを「新コスト要求」として数える簡略化でよい
  （返却込みの精密予測はYAGNI。サーバーが真実の情報源）
- スポイト（ミドルクリック）は既存ベルトの向きも拾うため、スポイト→即クリックは同型・同向きno-opとなり安全

## 想定操作フロー

- **引き直し**: 既存ベルトをスポイト → 即設置モード → そのベルト起点にドラッグ → 向き引き直し
- **アップグレード**: ビルドメニューで上位ベルト選択 → 旧ライン起点からドラッグ → 一括置き換え

## テスト方針

- サーバーCombinedTest:
  - 同型向き変え（コスト増減なし・搬送品の進行率維持）
  - 異種置き換え（差額精算: 旧コスト返却＋新コスト消費）
  - 非ファミリーブロックへのreplace要求拒否（改造クライアント防御）
  - インベントリ満杯時のセル失敗（アイテムロストなし）
  - isReplace無しの通常設置の既存挙動不変（Existsスキップ）
- クライアント: パス計算のリプレース判定ユニットテスト
- E2E: unity playtest（プレイテストDSL）でベルト設置→リプレースドラッグ→向き変更を確認

## 自己反証（この設計が拒否するケース）

- 機械の上からドラッグして機械を破壊 → ファミリー検証（クライアント・サーバー両方）で不可
- isReplace偽装で任意ブロック撤去・アイテム奪取 → サーバー側ファミリー検証で拒否
- 同じベルトを何度もドラッグ → no-op判定で冪等
- 許容とした挙動: 歯車ベルトへの置き換え時、歯車接続が無い場所でも設置自体は成立する（通常設置と同じ）

## 参照（調査済みの主要ファイル）

- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlacePointCalculator.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemSelector.cs`
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs`（L58-59のExistsスキップが拡張点）
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs`（返却ロジックの前例）
