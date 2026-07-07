# コネクタ種別（kind）による接続適合判定 設計

作成日: 2026-07-06

## 背景・課題

歯車・インベントリ・流体の接続は、すべて同一エンジン `BlockConnectorComponent<TTarget>`（`moorestech_server/.../Game.Block/Component/BlockConnectorComponent.cs`）に載っている。接続可否は `OnPlaceBlock` で**座標の一致だけ**で決まる:

> 自分のアウトプットコネクタが向いた座標 == 相手のインプットコネクタが向いた座標 → 接続成立

つまり **コネクタに「種別（これは歯なのか、軸なのか）」という概念が存在しない**。歯車では歯（teeth）と軸（shaft/axle）を、同じ offset に置いた2本のコネクタの `directions` ベクトルの違い（歯＝平面X/Y、軸＝Z）で暗黙表現しているだけで、`option`(`isReverse` 等)は接続成立「後」の挙動決定にしか使われない。

この「種別を幾何で暗黙表現する」構造が破綻すると、「シャフトと歯車の歯が直接つながる」等の**意図しない接続**が起きる。単セル・無回転なら幾何で分離できるが、多セル歯車・回転・斜め方向で種別が漏れる。加えて既知の穴が2つある:

- `directions == null` のコネクタは無条件で全方位接続し、`SelfConnector/TargetConnector` が **null 化**（後段NRE温床）
- `CalculateConnectPosToConnector` が同一ターゲット座標を**後勝ち上書き**（取りこぼし）

## 目的

接続の**意図（どの種別同士が繋がるべきか）をコネクタに明示**し、幾何が一致しても種別が不適合なら接続しないようにする。歯車・インベントリ・流体で共通の仕組みとする。

## 方針の決定事項

| 論点 | 決定 | 根拠 |
|---|---|---|
| 接続モデル | コネクタに種別(kind)を明示し、適合する種別同士だけ接続 | 意図を幾何から独立させ、多セル/回転でも堅牢 |
| 適合ルール | **受け入れリスト(accepts) + 相互同意**：`self.accepts ∋ target.kind` かつ `target.accepts ∋ self.kind` | 片側だけの受け入れによる一方的な意図しない接続を原理的に排除 |
| 適用範囲 | 共通 `IBlockConnector` 層（gear/inventory/fluid 全ドメイン） | 同一エンジンなので機構を分岐させない |
| データの置き場所 | マスタ（スキーマ） | 接続定義は既にスキーマ駆動。実行時ハードコードにしない |
| kind の型 | 文字列直値（等値マッチ） | YAGNI。タイプミス防止のマスタ定義+foreignKey化は将来拡張 |
| kind 未指定/accepts 空 | **接続しない**側に倒す | 曖昧なコネクタは繋がない（意図しない接続防止が目的） |
| 歯車の初期種別 | `teeth` / `axle` の2種。`teeth.accepts=[teeth]`, `axle.accepts=[axle]` | 現状 blocks.json をこの2つで分類可能 |
| 直交軸の歯車の噛み合い | teeth↔teeth に**実行時の軸平行チェック**を追加（kindだけでは防げない） | 回転軸は BlockDirection で決まる実行時プロパティで、静的 kind に焼き込めない（焼き込むと回転で不整合＝動的状態を静的定義に置く違反）。§3.5 参照 |
| クライアントプレビュー | 現状維持（サーバ権威）。非互換の可視化は今回スコープ外 | YAGNI |

## 設計

### 1. スキーマ変更

`VanillaSchema/blocks.yml` の `globalDefineInterface.IBlockConnector` に2フィールド追加（編集は edit-schema スキルに従い、SourceGenerator を再生成する）:

```yaml
- interfaceName: IBlockConnector
  properties:
  - key: connectorGuid
    type: uuid
  - key: offset
    type: vector3Int
  - key: directions
    type: array
    optional: true
    items:
      type: vector3Int
  - key: kind                 # 追加: このコネクタの種別
    type: string
    optional: true
  - key: accepts              # 追加: 接続を許可する相手kindの一覧
    type: array
    optional: true
    items:
      type: string
```

`gearConnects` / `inventoryConnects`（input・output）/ `fluidInventoryConnects`（inflow・outflow）は全て `implementationInterface: [IBlockConnector]` なので、再生成で自動的に `Kind` / `Accepts` プロパティを持つ。

### 2. 接続判定の変更（`BlockConnectorComponent.OnPlaceBlock`）

幾何マッチング成立後に**相互同意チェック**を追加する。擬似コード:

```
// 幾何一致した (self=outputConnector, target=targetElementConnector) について
bool KindCompatible(IBlockConnector self, IBlockConnector target)
    => self.Accepts != null && target.Accepts != null
    && self.Accepts.Contains(target.Kind)
    && target.Accepts.Contains(self.Kind);
```

- 幾何一致 かつ `KindCompatible` かつ **ドメイン適合述語**（あれば）成立の時だけ `isConnect = true`。
- `directions == null`（全方位受け入れ）経路でも、**connector を保持して kind 判定に通す**（下記§3の変更が前提）。null connector による無条件接続は廃止。

ドメイン適合述語は、生成側（テンプレート）から `BlockConnectorComponent` に**任意で注入**する `(selfConnector, targetConnector, selfBlockPositionInfo, targetBlock) => bool`。inventory/fluid は注入しない（kind のみ）。歯車テンプレートだけが §3.5 の軸チェック述語を注入する。汎用エンジンは kind 文字列の意味（teeth/axle）を知らないままで、ドメイン固有の知識は述語側に閉じる。

### 3. 判定ヘルパの変更（`BlockConnectorConnectPositionCalculator`）

現状 `directions == null` のとき `result.Add(pos, null)` としてコネクタ参照を捨てている。これを、**directions が null でもコネクタ参照を保持**する形に変える（「任意方向」を表すフラグ or センチネルで表現）。§2 の kind 判定と NRE 解消の前提。

`CalculateConnectPosToConnector` の戻り値を `Dictionary<Vector3Int, (position, connector)>` から `Dictionary<Vector3Int, List<(position, connector)>>` に変更し、同一ターゲット座標に複数の出力コネクタがある場合を保持。`OnPlaceBlock` 側は kind 適合するコネクタを選ぶ。

### 3.5. 歯車の軸適合（実行時チェック）

kind（静的）は「歯 vs 軸」の**役割**は表せるが、噛み合いに必要な**回転軸**は表せない。回転軸は `BlockDirection`（設置向き）で決まる実行時プロパティで、90度回転で Z→Y のように変わるため、静的な kind 文字列に焼き込むと回転で破綻する。

破れの具体例: 歯車A（North / 軸Z / 歯は XY平面）と 歯車B（UpNorth / 軸Y / 歯は XZ平面）を X方向（2平面の交線）に隣接させると、両者の teeth コネクタが X軸上で相互に向き合い、kind も teeth↔teeth で適合してしまう。物理的には軸が直交しており噛み合わない。

歯車テンプレートが注入するドメイン適合述語で、**teeth↔teeth のときのみ**次を追加要件とする（axle↔axle と item/fluid は1D/面の幾何で自己保護されるため対象外）:

```
axis(block) = block.BlockDirection.Rotate([0,0,1])   // 歯車の正準回転軸はZ。設置向きで実行時に導出
teethMeshOk =
    IsParallel(axis(selfBlock), axis(targetBlock))          // 回転軸が平行
    && Dot(targetPos - selfPos, axis(selfBlock)) == 0        // 接続方向が軸に垂直（軸方向に積まない）
```

軸は新規の静的データを増やさず既存の `BlockDirection` から導出する。述語は `kind == "teeth"` の対のときだけ上記を評価し、それ以外は true を返す。

### 4. マスタデータ移行

kind 未指定＝接続しない、としたので**全ドメインの既存コネクタに kind/accepts を付与**しないと既存の接続が壊れる（＝データ駆動で漏れを検出できる）。

- **歯車**: 既存 `directions` から推定してドラフト生成 → 目視レビューで確定
  - `directions` に X または Y の非ゼロ成分を含む gearConnect → `kind: teeth`, `accepts: [teeth]`
  - Z 軸のみ → `kind: axle`, `accepts: [axle]`
- **インベントリ**: 一律 `kind: item`, `accepts: [item]`（現状動作維持）
- **流体**: 一律 `kind: fluid`, `accepts: [fluid]`（現状動作維持）
- 対象: `moorestech_server/.../Tests.Module/.../forUnitTest/master/blocks.json` と `EditModeInPlayingTestMod/master/blocks.json`（テスト用データ）。本番 mod（`../moorestech_master`）の付与はユーザーが mooreseditor で別途。

推定スクリプトを scratchpad に用意し、生成後に人間が確認する。

### 5. 影響を受ける消費側（変更確認のみ）

- `GearConnect.FromConnectedInfo`（`IGearEnergyTransformer.cs`）: 引き続き `GearConnectsElement` にキャストして `Option`(isReverse) を読む。kind は接続可否のみに使い、回転計算（`GearNetwork.IsReverseRotation` / `TeethCount`）は不変。
- ベルト等の `InsertItemContext` の `SourceConnector/TargetConnector`: null connector 廃止により `.ConnectorGuid` 参照の NRE リスクが消える（副次改善）。

### 6. スコープ外

- GearChainPole の「ポール間チェーン接続」は隣接コネクタ engine とは別系統（`GearChainPlacementEvaluator` 等）。今回は対象外。
- クライアントの接続プレビュー可視化の kind 対応。

## テスト

`moorestech_server/.../Tests/CombinedTest/Core`・`UnitTest/Game` に追加:

- `teeth ↔ teeth`（同一軸・平面内隣接）は接続成立
- `teeth ↔ axle` は接続不成立（本件の主症状）
- `axle ↔ axle` は接続成立
- **軸が直交する teeth ↔ teeth（例: North軸Zの歯車と UpNorth軸Yの歯車を X方向に隣接）は接続不成立**（§3.5）
- **軸が平行な teeth ↔ teeth は接続成立**（回転して設置しても平面内隣接なら噛み合う）
- インベントリ `item ↔ item` は接続成立
- `directions == null` のコネクタでも kind 不適合なら接続されない／SelfConnector が非null
- 既存の `GearNetworkTest` / `BeltConveyorTest` 等がグリーンのまま（移行データ付与後）

## 検証

- `uloop compile --project-path ./moorestech_client`
- `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(Connector|Gear|BeltConveyor|Fluid|InsertItemContext)"`
