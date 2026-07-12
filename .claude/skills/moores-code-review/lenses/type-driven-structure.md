---
keywords:
  - "readonly struct"
  - "Context"
  - "interface I"
  - "MessagePackObject"
extensions:
  - .cs
model: opus
---

# Lens: 型による不正状態の排除と配置構造（PR987/996/997由来）

## あなたの役割
cwdを読み、patchが**「規約上ありえない状態」を型で排除せず、規律頼みにしている**Critical、および**データクラスの配置規約違反**のCriticalのみを返す。

## 検査対象の絞り込み
起動prompt 2行目 `Patch path` をReadし、新規struct/class/interfaceの定義と、既存Context/Selector系へのフィールド追加に絞る。

## Critical判定基準

### 1. 共用体的struct（どれか1つが埋まっていたら有効）
- 判別子enum＋「種別ごとに一部しか使われないフィールド群」を並置するstruct/classの**新設**。利用側が `switch(EntryType)` で無効フィールドを `default`/`null` として無視する構造は、種別追加のたびに全利用側が壊れる。
- **正解形**: 判別子＋種別ごとのペイロード型（`IXxxPayload` または static factoryで有効フィールドのみ設定）。前例: `RailConnectionEditRequest`（`RailEditMode`＋static factory）、`BlueprintRequest`（`BlueprintOperation`＋private ctor＋static factory）。

### 2. Context/Selectorへの無秩序なフィールド追加（god object化）
- 既存のContext構造体・Selectorに、種別固有のフィールド/プロパティ/コンストラクタ引数を**さらに並べて追加**するpatch。追加そのものより「種別ごとの排他フィールドが5個を超えて増殖する構造の温存」を見る。
- **正解形**: 判別子＋ペイロード型への分割、SelectorはDictionary/ファクトリ登録で分岐を閉じる。既に「後で対応」合意がある箇所（`PlaceSystemUpdateContext` 等）への追記は、悪化させる追加のみCritical（現状維持の1フィールドはWarning備考）。

### 3. N択1の必須役割をnull許容で表現
- 「必ずどれかの役割に紐づく」コンポーネントが、役割を複数のnullableフィールドやoptional引数で持つ形。
- **正解形**: 各役割interfaceの親として束ねinterfaceを定義し、ctorで必須引数にする。前例: `IElectricEnergyRole`（`IElectricConsumer`/`IElectricGenerator`/`IElectricTransformer` の親、`ElectricWireConnectorComponent` のctorで必須）、`IOpenableBlockInventoryComponent`。

### 4. データクラスの配置
- `Server.Protocol/PacketResponse/` 直下に `IPacketResponse` 実装以外の.cs（`*Dto.cs`・MessagePack専用ファイル）を**新規に**置くのはCritical。DTOは別階層（`Server.Util/MessagePack/` か専用サブディレクトリ）へ。プロトコル横断で使わないRequest/Responseはプロトコルクラス内ネスト（`#region MessagePack`）が標準形。
- ディレクトリ肥大: 同一機能のUtilディレクトリが10ファイルに迫ったら、依存の下流（純粋判定）→上流（オーケストレーション）が見えるサブディレクトリへ分割。前例: `Util/ElectricWire/` の `Placement/`→`Connection/`→`AutoConnect/` 構成、`Util/InventoryService/`。

## Criticalにしないもの（過検知ガード）
- フィールドがすべての種別で有効なデータキャリア（共用体ではない）。
- 2種別・フィールド3個以下の小さなstruct — 分割コストが上回る。備考に留める。
- 既存構造の単純利用（このpatchが構造を新設・悪化させていないもの）。

## 依頼動詞優先ガード
起動prompt 3行目 `User prompt` をRead。「許容するトレードオフ」（差分を抑えるため現構造維持等）が合意済みなら指摘せず、備考1行に留める。

## 出力フォーマット
Criticalが1件でもあれば `Critical: あり`、0件なら `Critical: なし`。
続けて `修正方針:` に `- <ファイル:行>: <どの型分割/束ねinterface/配置先で不正状態を排除するか（最小修正）>` を1行ずつ列挙する。
