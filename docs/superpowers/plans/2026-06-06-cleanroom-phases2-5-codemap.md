# クリーンルーム フェーズ2〜5 コード変更 俯瞰書（コードマップ）

- 日付: 2026-06-06
- 対象: moorestech V8 mod クリーンルーム（空気純度）システム フェーズ2〜5
- 種別: **俯瞰コード変更書（コードマップ）**。ファイル／クラス／メソッドシグネチャ＋データフロー＋統合点のレベルで「どこに何を足すか」を示す。各タスクのTDD手順は展開しない。
- **改訂: v2（2026-06-06 レビュー第1回反映）** — データストア化／プッシュ型／ブロック命名／単一エアフィルター を反映。

> **⚠ 整合性の状態（必読）**
> 本 v2 改訂で、フェーズプラン群（`-phase2-purity-sim` / `-phase3-purifier-pollution` / `-phase4-machine-integration` / `-phase5-io-save`）とバランス確定書・フェーズ4依存メモは**この俯瞰書に対して陳腐化（stale）**した。旧名（`CleanRoomPurityService` / `ICleanRoomMachineGate` / `Door` / `AirPurifier`）やプル型ゲートのまま。**設計レビューが収束してから一括で再生成する**方針（レビュー途中で重い詳細プランを作り直すと無駄が多いため）。本俯瞰書が現時点の正。

> **このドキュメントの位置づけ**
> - フェーズ1（境界ブロック＋3D密閉部屋検出）の詳細TDDプランは `2026-06-05-cleanroom-phase1-detection.md`。ただし下記§1の通り、検出も含めて**世界システムは新規作成**する（データストア化）。
> - 設計の根拠・ゲーム仕様は `2026-06-05-cleanroom-design.md`。本書は「設計書のどの項を、既存コードのどの型にどう載せるか」の対応表。
> - 実装着手時は本書を入力に各フェーズを **superpowers:writing-plans** で TDDプランへ落とす。

---

## 0. 前提・依存関係・ブロック一式

### 必要なブロック（レビュー第1回で確定）

| # | ブロック | blockType | 役割 | 境界か |
|---|---|---|---|---|
| 1 | クリーンルーム壁 | `CleanRoomWall` | 空間を密閉する壁 | 境界 |
| 2 | クリーンルームエアフィルター | `CleanRoomAirFilter` | 空気を処理し不純物を除去（電力＋フィルター消費） | 内部 |
| 3 | クリーンルームドアハッチ | `CleanRoomDoorHatch` | 人の出入り。気密境界＋通過バースト汚染 | 境界 |
| 4 | クリーンルームアイテムハッチ | `CleanRoomItemHatch` | アイテム搬入出。気密境界＋搬送レート汚染 | 境界 |
| 5 | クリーンルームパイプハッチ | `CleanRoomPipeHatch` | 流体搬入出（超純水/IPA等）。気密境界 | 境界 |
| 6 | クリーンルーム専用機械 | `CleanRoomMachine` | 半導体製造（EUV露光等）。有効な部屋内でのみ稼働、純度で出力グレード制限 | 内部 |

- **境界ブロック**（壁/ドアハッチ/アイテムハッチ/パイプハッチ）＝flood-fill上の**気密境界**。`CleanRoomBoundaryKind { Wall, DoorHatch, ItemHatch, PipeHatch }`。
- **内部ブロック**（エアフィルター/専用機械）＝部屋の中に置く機能ブロック。

### 依存

| 依存 | 内容 | 影響 |
|---|---|---|
| **検出ロジック（新規作成）** | `CleanRoom`（Cells, V, S, 有効フラグ＋純度状態）／`CleanRoomDetector`（純関数flood-fill）／境界ブロック群／`CleanRoomBoundaryKind` を**新規に作る**（フェーズ1プランの相当物を、データストア前提で作り直す） | フェーズ2以降はこの土台に載る |
| **アップグレードのグレード機構**（`2026-06-05-upgrade-system-design.md`） | グレード＝**独立 ItemId**（`ICチップ_Lv1..Lv4`、決定的GUID、SourceGenerator）。出力レベル分布抽選は §7.2 で順序固定。アップグレードのフェーズB（品質軸）は**未計画・未実装** | フェーズ4が「レベル抽選コア」を半導体限定で内包する（決着メモ参照） |

**確認済みのコードベース事実**
- 既存の世界システムは**データストア方式**: `GearNetworkDatastore`（`GearNetwork` を保持・`GameUpdater.UpdateObservable` 購読・`AddGear/RemoveGear`・`GetGearNetwork(BlockInstanceId)`）、`WorldEnergySegmentDatastore`、`FluidMapVeinDatastore`。クリーンルームもこれに倣う。
- 電力は**プッシュ**: `EnergySegment` が毎tick `IElectricConsumer.SupplyEnergy(power)` をブロックへ呼ぶ。クリーンルームの効果も同じくプッシュにする（後述§1.4）。
- `ItemStack` に品質フィールドは無い → グレードは独立 ItemId（アップグレード設計書§3）。

---

## 1. 横断アーキテクチャ（データストア方式・プッシュ型）

### 1.1 `CleanRoomDatastore` — 世界システム（歯車/電力データストアと同型）

クリーンルーム系の中核は `CleanRoomDatastore`（DI singleton, eager）。`GearNetworkDatastore` と同じ骨格:

- **ブロックの設置/削除を購読**（`WorldBlockUpdateEvent.OnBlockPlaceEvent/OnBlockRemoveEvent`）し、影響範囲の部屋を再検出（dirtyに積み、tickで分割処理）。
- **`GameUpdater.UpdateObservable` を購読**し、毎tick各部屋の純度を更新。
- `CleanRoom` 群と「ブロック→部屋」マップを保持。`GetCleanRoom(BlockInstanceId)` / `TryGetCleanRoom(...)` / `TryGetCleanRoomAt(Vector3Int)` を公開。
- セーブ/ロード対象（純度状態の永続化、§1.3）。

検出は純関数 `CleanRoomDetector`（6近傍flood-fill、AABB局所化）に委譲。データストアが結果を受けて `CleanRoom` を作る。

### 1.2 `CleanRoom` — 部屋（純度状態を内包）

`CleanRoom` 自身が幾何＋純度を持つ（旧 v1 の `CleanRoomPurityState` 分離は廃止）:

```csharp
public class CleanRoom
{
    public IReadOnlyCollection<Vector3Int> Cells { get; }
    public int Volume { get; }          // V（空セル数。機械/フィルター占有セルは除外）
    public int SurfaceArea { get; }     // S
    public bool IsValid { get; }

    // 純度状態（データストアが毎tick更新、再検出/ロードで引き継ぐ）
    public double ImpurityCount { get; private set; }   // N
    public CleanRoomRoomStatus Status { get; private set; }  // Valid/Degraded/Invalid
    public double Concentration => Volume > 0 ? ImpurityCount / Volume : 0;  // C = N/V

    public void AddImpurity(double delta);
    public void RemoveImpurity(double removed);
    public void SetStatus(CleanRoomRoomStatus s, double graceSeconds);
}
public enum CleanRoomRoomStatus { Valid, Degraded, Invalid }
```

> **清浄度は濃度 C のみで表す**（レビュー反映）。名前付きの「クラス」型（`CleanRoomClass`）は持たない。最大グレード天井・down-bin率は C/ACH をマスタ閾値（`cleanRoomThresholds.yml`）に照らして算出する（§4 の `CleanRoomEffectResolver`）。

### 1.3 部屋同一性と永続化（Cells 重なり ＋ ブロック後に復元）

- **再検出時**: データストアは旧 `CleanRoom` 群を退避し、新検出結果と**最大セル重なり**で対応付け、N を引き継ぐ（結合→合算／分割→濃度按分 `N=C·V_new`）。`CleanRoom.Id` は永続キーにしない。
- **永続化**: `RailGraphSaveLoadService`（鉄道）が前例。3点改修：
  1. `Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs` … `[JsonProperty("cleanRoom")] List<CleanRoomSaveData>` 追加。
  2. `Game.SaveLoad/Json/AssembleSaveJsonText.cs#AssembleSaveJson()` … `_cleanRoomDatastore.GetSaveData()` を追加。
  3. `Game.SaveLoad/Json/WorldLoaderFromJson.cs#Load()` … **`_worldBlockDatastore.LoadBlockDataList` の後**に `_cleanRoomDatastore.Restore(load.CleanRoom)`。
- `CleanRoomSaveData` … `N` ＋状態(Valid/Degraded/Invalid) ＋ `Cells` 署名（再導出できる V/S・効果は保存しない）。復元はブロックロード→検出→セル重なりでN照合。

### 1.4 部屋の効果は **ブロックへプッシュ**（電力と同型・ゲート廃止）

機械等がプル（`ServerContext.GetService` や "Gate"）で部屋を問い合わせるのではなく、**`CleanRoomDatastore` が毎tick、各内部ブロックへ部屋の効果をプッシュ**する。`EnergySegment.SupplyEnergy` と同じ向き。

- 受信側インターフェース `ICleanRoomStateReceiver`（**`Game.Block.Interface/Component`** に置く。プリミティブのみ＝Game.Block はクリーンルーム型を知らない）:

```csharp
// 部屋の効果を受け取るブロック側コンポーネント。データストアが毎tickセットする
// Block-side receiver; the datastore pushes the room effect each tick
public interface ICleanRoomStateReceiver : IBlockComponent
{
    void SetCleanRoomEffect(CleanRoomEffect effect);
}

// プッシュされる最小ペイロード（算出済みの効果値）
// Minimal pushed payload (already-resolved effect values)
public readonly struct CleanRoomEffect
{
    public readonly bool InValidRoom;   // 有効な部屋内にあるか（Invalid/部屋外なら false）
    public readonly int  MaxGrade;      // 最大チップグレード天井
    public readonly double DownBinRate; // 汚れ由来の格下げ率
}
```

- **依存方向**: `Game.CleanRoom → Game.Block.Interface`（データストアが受信IFへプッシュ）。`Game.Block` は `Game.CleanRoom` を参照しない。C/ACH→`MaxGrade`/`DownBinRate` の解決はデータストア側（`Game.CleanRoom`）でマスタ閾値に照らして行い、結果だけブロックへ渡す。
- 内部ブロック（専用機械・エアフィルター）は設置時にデータストアの「ブロック→部屋」マップへ登録（`GearNetworkDatastore.AddGear` 相当）。

---

## 2. フェーズ2 — 純度シミュレーション（データストアに実装）

**目的:** `CleanRoom` に濃度 `C=N/V` を持たせ、`dN/dt = A_total − n·q·C` で平衡 `C_eq=A_total/(n·q)` に収束。二条件（C閾値＋ACH）＋ヒステリシスで効果（最大グレード/down-bin）、Valid/Degraded/Invalid＋猶予を運用、再検出/セーブで継続。汚染源 A はデータストアが直接算出（実係数はフェーズ3）、除去 n·q は部屋内エアフィルターを読む。

### File Structure
- Create: `Game.CleanRoom/CleanRoomDatastore.cs` — 世界システム（購読・検出呼び出し・純度tick・永続化）
- Create: `Game.CleanRoom/CleanRoom.cs` — 部屋（幾何＋純度、§1.2）
- Create: `Game.CleanRoom/CleanRoomDetector.cs` — flood-fill 純関数
- Create: `Game.CleanRoom/CleanRoomPurityRules.cs` — 純度判定（二条件＋ヒステリシス、純関数。C/ACH→効果・状態。名前付きクラス型は持たない）
- Create: `Game.Block.Interface/Component/ICleanRoomAirFilter.cs` — n·q をデータストアが読むための口（フェーズ3のエアフィルターが実装。**Block.Interface 側**＝asmdef境界越えに必要なので残す）
- Create: `Game.CleanRoom/SaveLoad/CleanRoomSaveData.cs`
- Modify: `WorldSaveAllInfoV1.cs` / `AssembleSaveJsonText.cs` / `WorldLoaderFromJson.cs`（3点）
- Modify: `Server.Boot/MoorestechServerDIContainerGenerator.cs` — `CleanRoomDatastore` 登録＋eager
- マスタ: `VanillaSchema/cleanRoomThresholds.yml`（新規・C→最大グレード/down-bin/必要ACH）＋`Core.Master/CleanRoomThresholdMaster.cs`＋`_CompileRequester.cs`

### tick アルゴリズム（`CleanRoomDatastore.Update`）
各 `CleanRoom` について毎tick:
1. `V/S` は検出結果から。接続点数＝部屋の境界ブロックを `CleanRoomBoundaryKind` 別集計。
2. `A_total`（データストアが部屋の幾何・境界種別・内部ブロック状態から直接算出。フェーズ2では 0 可、実係数はフェーズ3）。
3. `nq = Σ` 部屋内エアフィルターの `RemovalVolumePerSecond`（フェーズ2では 0）。
4. `dN = (A_total − nq·C)·SecondsPerTick`（=×0.05）→ `AddImpurity/RemoveImpurity`、N は0クランプ。
5. 効果＝`C` と `ACH=nq/V` をマスタ閾値に照らして最大グレード天井・down-bin率を決定（二条件＋ヒステリシス：上げ/下げ別閾値）。名前付きクラス中間型は持たない。
6. Valid/Degraded/Invalid＋猶予（密閉崩壊/純度急落でDegraded、猶予内回復でValid、猶予切れInvalid）。

### データフロー
```
WorldBlockUpdateEvent(設置/削除) ─▶ CleanRoomDatastore（dirty積み→CleanRoomDetectorで再検出→CleanRoom群更新、Cells重なりでN引き継ぎ）
GameUpdater tick ─▶ CleanRoomDatastore.Update（各CleanRoomの N→C→効果→段階）
   ├─ A_total（データストアが直接算出。実係数はフェーズ3）
   └─ ICleanRoomAirFilter（n·q。フェーズ3供給）
セーブ/ロード: AssembleSaveJsonText／WorldLoaderFromJson ⇄ CleanRoomDatastore.GetSaveData/Restore（ブロック後・Cells重なり）
```

### 実装プランで確定（フェーズ2）
閾値（C→最大グレード/down-bin/必要ACH）・ヒステリシス幅・N按分規則・猶予秒数・Cells署名方式（全セル集合 vs アンカー）。

---

## 3. フェーズ3 — エアフィルター ＋ 電力 ＋ 汚染源

**目的:** `n·q` を供給するエアフィルターブロックを作り、汚染源を `A_total` に実供給。フェーズ2の注入口を埋める。

### File Structure
- マスタ: `VanillaSchema/blocks.yml` に `CleanRoomAirFilter` 追加（param: `q`, `requestPower`, フィルタースロット）＋`_CompileRequester.cs`＋テストmod
- Create: `Game.Block/Blocks/CleanRoom/CleanRoomAirFilterComponent.cs` — **まず単一コンポーネントで実装**（電力消費＋フィルター仕事量消費＋q処理を1つに）。`IElectricConsumer, IUpdatableBlockComponent, IBlockSaveState, ICleanRoomAirFilter`。**分割（電力/フィルター/セーブの細分）は後から**（コメント3）
- Create: `Game.Block/Factory/BlockTemplate/VanillaCleanRoomAirFilterTemplate.cs` ＋ `VanillaIBlockTemplates.cs` 登録
- Create: `Game.CleanRoom/Pollution/CleanRoomPollutionCalculator.cs` — A_total 算出ヘルパ（`CleanRoomDatastore` が利用。注入インターフェースは作らず必要に応じ具体クラス）

### キー型（単一コンポーネント）
```csharp
// エアフィルター（初版は単一コンポーネント。電力割合で実効q、除去量でフィルター消費）
// Air filter (initially one component): effective q scales with power, filter wears by removed amount
public class CleanRoomAirFilterComponent
    : IElectricConsumer, IUpdatableBlockComponent, IBlockSaveState, ICleanRoomAirFilter
{
    public ElectricPower RequestEnergy => new ElectricPower(_requestPower);
    public void SupplyEnergy(ElectricPower power);          // 電力割合を保持
    public double RemovalVolumePerSecond { get; }           // = q × 電力割合 × (フィルター残>0 ? 1 : 0)
    public void ApplyRemovedImpurity(double removed);       // データストアが除去量を渡す→フィルター摩耗
    public void Update();
    public string GetSaveState();                           // フィルター残量
}
```

- **自動電力接続**: `IElectricConsumer` なので設置時に既存 `ConnectMachineToElectricSegment` が近傍ポールの `EnergySegment` へ自動登録。
- **電力割合換算**は既存 `MachineCurrentPowerToSubSecond.GetSubTicks` の流儀。
- **フィルター消費**: データストアが各フィルターの除去寄与を `ApplyRemovedImpurity` で渡す（フィルターは C を知らない）。累計が `filterCapacity` ごとに1個消費。残0で `RemovalVolumePerSecond=0`。

### 汚染源（`CleanRoomPollutionCalculator`）
`A_total = A_machine + A_hatch + A_door + a_volume·V + a_surface·S + a_connector·接続点数`。
- `A_machine`: 部屋内 `CleanRoomMachine` の稼働中フラグ×係数。
- `a_volume/a_surface`: `CleanRoom.Volume/SurfaceArea`。
- `a_connector·count`: `CleanRoomBoundaryKind` 別集計（DoorHatch/ItemHatch/PipeHatch）。
- `A_hatch/A_door`: フェーズ5でハッチ/ドアハッチが計量を供給（フェーズ3では 0）。

### データフロー
```
設置 ─▶ ConnectMachineToElectricSegment ─▶ EnergySegment.SupplyEnergy ─▶ CleanRoomAirFilterComponent（実効q）
CleanRoomDatastore.Update ─ 部屋内フィルターの RemovalVolumePerSecond 合算 ─▶ n·q ─▶ N更新 ─ 除去量配分 ─▶ ApplyRemovedImpurity（フィルター摩耗）
```

### 実装プランで確定（フェーズ3）
q・filterCapacity・requestPower・汚染係数（バランス確定書）。高級フィルター種別の有無。

---

## 4. フェーズ4 — 専用機械統合（天井 ＋ down-bin ＋ Invalid停止・プッシュ受信）

**目的:** `CleanRoomMachine` の出力を部屋の純度に連動。(a) 部屋がInvalid/部屋外なら稼働停止、(b) 純度（C/ACH）が最大グレード天井、(c) 汚れでdown-bin。**部屋効果はプッシュで受け取る**（ゲート廃止）。

### ⚠ グレード依存（決着メモ参照）
レベル抽選（独立ItemId）はアップグレードのフェーズB＝未計画。よってフェーズ4が**半導体限定のレベルファミリー＋出力レベル分布抽選**をアップグレード §7.2 順序・決定的乱数に準拠して内包する（`2026-06-06-cleanroom-phase4-grade-dependency.md` 案B）。

### File Structure
- マスタ: `CleanRoomMachine` blockType ＋ `ICチップ_Lv1..Lv4`（決定的GUID・レベルファミリー）＋露光レシピのレベル分布テーブル枠
- Create: `Game.Block.Interface/Component/ICleanRoomStateReceiver.cs` ＋ `CleanRoomEffect`（§1.4・**Block.Interface**）
- Create: `Game.Block/Blocks/CleanRoom/CleanRoomStateReceiverComponent.cs` — 受信値（CleanRoomEffect）を保持。`CleanRoomMachine` に合成
- Modify: `Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs`（または `CleanRoomMachine` 用プロセッサ）— `Idle/Processing` で受信した `InValidRoom` を見て停止（電力0と同じ「止まるが壊れない」、Degraded猶予中は稼働継続）
- Modify: `Game.Block/Blocks/Machine/Inventory/VanillaMachineOutputInventory.cs#InsertOutputSlot` — 出力ItemId生成直前に、受信 `MaxGrade`/`DownBinRate` で天井クランプ＋down-bin抽選
- Create: `Game.CleanRoom/Machine/CleanRoomEffectResolver.cs` — C/ACH/状態→`CleanRoomEffect`（MaxGrade/DownBinRate/InValidRoom）をマスタ閾値で算出。データストアがこれで算出し受信IFへプッシュ

### 統合点
- **停止＆効果はプッシュ受信**: `CleanRoomDatastore` が毎tick、部屋内の `CleanRoomMachine`（`ICleanRoomStateReceiver`）へ `CleanRoomEffect` をプッシュ。機械は自分の受信値を読むだけ（クロスasmdef呼び出し無し、"Gate" 無し）。
- **天井＋down-bin**: 出力レベル抽選（§7.2 ③）を機械側で実行：`ceiling(MaxGrade) → 基礎分布抽選 → down-bin(DownBinRate) → Lv確定`。乱数はアップグレード流儀の決定的シード（`_processedCycleCount`+`_blockInstanceId`）。
- **multi-block 占有**: データストアが機械の `BlockPositionInfo.MinPos..MaxPos` 全占有セルが同一有効部屋にあるか判定し、無ければ `InValidRoom=false` をプッシュ。

### データフロー
```
CleanRoomDatastore.Update ─ CleanRoomEffectResolver(C/ACH→効果) ─▶ ICleanRoomStateReceiver.SetCleanRoomEffect（プッシュ）
CleanRoomMachine プロセッサ ─ 受信 InValidRoom=false なら停止
InsertOutputSlot ─ 受信 MaxGrade/DownBinRate で ceiling→分布抽選→down-bin→ItemId確定
```

### 実装プランで確定（フェーズ4）
純度閾値ごとの最大グレード・down-bin率（バランス確定書）。抽選合成順序・乱数源一本化（§7.2）。またがり機械の判定規則。

---

## 5. フェーズ5 — ハッチ挙動 ＋ セーブ仕上げ

**目的:** 境界の3ハッチ（ドアハッチ＝人／アイテムハッチ＝アイテム／パイプハッチ＝流体）の搬送挙動を実装し、汚染計量を供給。永続化を仕上げる。**全ハッチは気密境界**（壁同様に密閉）で、物質/人はコンポーネント論理と離散イベントで越える。

### File Structure
- Create: `Game.Block/Blocks/CleanRoom/CleanRoomItemHatchComponent.cs` — `IBlockInventory`。壁貫通でアイテム中継＋`RecentThroughputPerSecond`（A_hatch）
- Create: `Game.Block/Blocks/CleanRoom/CleanRoomPipeHatchComponent.cs` — `IFluidInventory, IUpdatableBlockComponent`（流体push型）。壁貫通で流体中継
- Create: `Game.Block/Blocks/CleanRoom/CleanRoomDoorHatchComponent.cs` — `NotifyPlayerPassage()` で `A_door` バースト計上（気密境界のまま）
- Modify: 境界テンプレート（`CleanRoomBoundaryKind` switch でハッチ種にコンポーネント合成）＋ blocks.yml にハッチのコネクタparam
- Modify: `Game.CleanRoom/Pollution/CleanRoomPollutionCalculator.cs` — A_hatch（レート）/A_door（バースト）取り込み

### 統合点
```csharp
public class CleanRoomItemHatchComponent : IBlockInventory, IUpdatableBlockComponent, IBlockSaveState
{
    public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context);
    public double RecentThroughputPerSecond { get; }  // A_hatch 用レート窓
}
public class CleanRoomPipeHatchComponent : IFluidInventory, IUpdatableBlockComponent, IBlockSaveState
{
    public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source);
}
```
- アイテム中継は `VanillaBeltConveyorBlockInventoryInserter`／`BlockConnectorComponent<IBlockInventory>` の流儀。流体は `FluidPipeComponent`／`IFluidInventory.CreateFluidInventoryConnector` の流儀（push型のため Update 必須）。
- プレイヤー通過は座標監視の自動検知が無いため、サーバー側 `NotifyPlayerPassage()` で受ける（テストは直接呼ぶ）。
- セーブは各コンポーネントの `IBlockSaveState`（中継中アイテム/流体）。グローバルスキーマ非改変（`FuelGearGeneratorItemComponent` と同方式）。コネクタ再リンクは `BlockConnectorComponent` のコンストラクタが行う（`IPostBlockLoad` 不要）。

### 実装プランで確定（フェーズ5）
ハッチのスループット上限・レート窓長・ドアバースト量・多重通過合算・I/Oセーブ形式。

---

## 6. 変更ファイル総覧

| 区分 | ファイル | 種別 | フェーズ |
|---|---|---|---|
| マスタ | `VanillaSchema/cleanRoomThresholds.yml` ＋ `Core.Master/CleanRoomThresholdMaster.cs` | 新規 | 2 |
| マスタ | `VanillaSchema/blocks.yml`（6ブロック）＋ `_CompileRequester.cs` | 改 | 1,3,4,5 |
| マスタ | `ICチップ_Lv1..Lv4` レベルファミリー＋露光レシピ分布 | 新規 | 4 |
| 世界系 | `Game.CleanRoom/CleanRoomDatastore.cs` / `CleanRoom.cs` / `CleanRoomDetector.cs` / `CleanRoomPurityRules.cs` | 新規 | 1,2(拡張) |
| 汚染算出 | `Game.CleanRoom/Pollution/CleanRoomPollutionCalculator.cs` | 新規 | 3(5で拡張) |
| 注入口 | `Game.Block.Interface/Component/ICleanRoomAirFilter.cs` | 新規 | 2 |
| プッシュ | `Game.Block.Interface/Component/ICleanRoomStateReceiver.cs` ＋ `CleanRoomEffect` | 新規 | 4 |
| プッシュ | `Game.Block/Blocks/CleanRoom/CleanRoomStateReceiverComponent.cs` ／ `Game.CleanRoom/Machine/CleanRoomEffectResolver.cs` | 新規 | 4 |
| ブロック | `CleanRoomAirFilterComponent.cs`（単一）＋ `VanillaCleanRoomAirFilterTemplate.cs` | 新規 | 3 |
| ブロック | ハッチ3種 Component ＋ 境界テンプレート | 新規/改 | 1,5 |
| 機械 | `VanillaMachineProcessorComponent.cs` / `VanillaMachineOutputInventory.cs`（CleanRoomMachine用フック） | 改 | 4 |
| セーブ | `Game.CleanRoom/SaveLoad/CleanRoomSaveData.cs` ＋ 3点改修（`WorldSaveAllInfoV1`/`AssembleSaveJsonText`/`WorldLoaderFromJson`） | 新/改 | 2 |
| 登録 | `VanillaIBlockTemplates.cs` ／ `Server.Boot/MoorestechServerDIContainerGenerator.cs` | 改 | 各 |
| テスト | `Tests/CombinedTest/Core/CleanRoom*Test.cs` | 新規 | 各 |

---

## 7. リスクと留意点

- **同期即時の全再検出は禁止**。データストアは dirty 積み→tick分割。AABB局所化（触れた壁AABB+1）＋ `MaxRoomVolume` 安全網。
- **効果の揺れ**: ヒステリシス＋Degraded猶予の二段。結合/分割直後の濃度按分は閾値境界に注意。
- **フェーズ4のグレード依存**: アップグレードのフェーズB未計画。フェーズ4が抽選コアを内包し、§7.2 順序・決定的乱数を将来Bへ引き渡す（決着メモ §6）。
- **占有セルと V**: 機械/エアフィルターの占有セルは V から除外。境界ブロックは内部Vに影響せず `a_connector` で寄与。

---

## 7.9 設計判断の記録（技術メモ）

- **データストア方式の根拠**: 歯車/電力/流体の世界系がすべて `Datastore`（保持＋tick購読＋設置/削除購読）。クリーンルームも同型で `CleanRoomDatastore`。検出も含めて新規作成（フェーズ1相当物を作り直す）。
- **プッシュ型の根拠**: 電力 `EnergySegment.SupplyEnergy` と同じく、データストアがブロックへ効果をプッシュ。プル（ServerContext/ゲート）は使わない。依存方向は `Game.CleanRoom → Game.Block.Interface`、`Game.Block` はクリーンルーム型非依存（プリミティブの `CleanRoomEffect` のみ受信）。
- **エアフィルターは単一コンポーネント初版**: 電力/フィルター/セーブの細分は後から。
- **名前付き「クラス」型は持たない（レビュー）**: 清浄度は濃度 C で表し、最大グレード/down-bin は C/ACH をマスタ閾値（`cleanRoomThresholds.yml`）に照らして算出。`CleanRoomClass` 列挙は廃止（紛らわしいため）。状態列挙 `CleanRoomRoomStatus`(Valid/Degraded/Invalid) は別概念として残す。
- **A_total の注入インターフェースは作らない（レビュー）**: `ICleanRoomPollutionInput` は廃止。`CleanRoomDatastore` が直接算出（必要なら具体ヘルパ `CleanRoomPollutionCalculator`）。細部は必要に応じて作る。なお `ICleanRoomAirFilter` は asmdef 境界越えにデータストアが n·q を読むため残す。
- **グレード抽選API（フェーズ4が公開＝将来アップグレードBの入力）**: 分布テーブル `IReadOnlyList<(int level, double weight)>`、`ResolveOutputItemId(recipe, maxGrade, downBinRate, long seed)`、合成順序 `ceiling→base-draw→[品質シフト挿入点]→down-bin→確定`、単一決定的シードを splitmix64 でサブストリーム分離。
- **パイプハッチ**は流体push型のため `IUpdatableBlockComponent` も実装。
- **全ハッチは気密境界**（flood-fill上は壁同様）。人通過は `NotifyPlayerPassage()`（自動クロス検知は範囲外）。
- **環境**: `GameUpdater` は UniRx。`SecondsPerTick=0.05`、テストの時間送りは `GameUpdater.RunFrames(uint)`。
- **永続化復元はブロック後**（`RailGraphSaveLoadService` と同位置）。

---

## 8. 次アクション

1. レビューが収束したら、本俯瞰書 v2 を入力にフェーズ2〜5の詳細TDDプラン＋バランス確定書を**一括再生成**（現行のフェーズプランは v2 に対して stale）。
2. フェーズ4着手前にアップグレードのレベル抽選状況を再確認（決着メモ）。
3. 各フェーズ完了時に `uloop run-tests --filter-value "CleanRoom"` で回帰。
