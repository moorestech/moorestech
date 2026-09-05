# ポンプUI（油井・歯車ポンプ）と鉱脈判定の採掘機パリティ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 油井（`ElectricPump`）と歯車ポンプ（`GearPump`）を開けるようにし、Web UIに内部タンク・動力・公称生成速度・鉱脈警告を出す。あわせてポンプの鉱脈判定と設置制限を採掘機と同じ底面フットプリントXZ規則へ統一する。

**Architecture:** 採掘機の3層と同型に組む。①共有判定 `PumpVeinFootprintJudge`（`Game.Block.Interface/Vein`、`MinerVeinFootprintJudge` の隣）をサーバーの汲み上げ対象決定とクライアントの設置制限が共用する。②サーバーは新設 `PumpStateComponent` が `PumpBlockStateDetail`（汲み上げ中流体と公称生成量）を配信し、油井は `ElectricPumpComponent` が `CommonMachineBlockStateDetail`（実効要求電力・稼働状態）を、内部タンクは `PumpFluidOutputComponent` が `FluidMachineInventoryStateDetail` を配信する。③Web UIホストが `PumpDetailDto` を組み、Web側は `PumpSection` を `SectionStackView` の汎用合成に載せる。新プロトコルは作らない。

**Tech Stack:** Unity 2022 / C# / NUnit / uloop CLI / React + Mantine + zod + vitest + Playwright（`moorestech_web/webui`）/ moorestech_master（JSON）

## Requirements

設計対話（2026-09-05）で確定した要件。正本は `docs/adr/0051-pump-ui-and-vein-footprint-parity-with-miner.md`。

1. 油井・歯車ポンプがインタラクトで開ける。受け入れ基準: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json` の両ブロックで `blockUIAddressablesPath` が空でない。
2. ポンプUIに内部タンク（液種・量・容量）が出る。受け入れ基準: `block_inventory.current` の `fluidSlots` に出力タンク1本が載り、Web UIの `pump-fluid-slots` が描画される。
3. 油井UIに電力充足率と稼働状態ラベルが出る（ADR 0010準拠、実効要求電力）。受け入れ基準: `CommonMachineBlockStateDetail.RequestPower` が待機中は `requiredPower × idlePowerRate`、稼働中は `requiredPower`。`CurrentStateType` は稼働中 `"processing"`、それ以外 `"idle"`。
4. 歯車ポンプUIに歯車のRPM・トルク（既存 `GearSection`）が出る。受け入れ基準: `PumpDetailDto` に電力サブオブジェクトが無く、`GearDetailDto.BaseRpm/BaseTorque` が `GearPumpBlockParam.GearConsumption` から埋まる。
5. ポンプUIに公称生成速度（充足率100%時の分間量）が流体ごとに出る。受け入れ基準: `PumpingFluidDto.AmountPerMinute == amount / generateTime × 60`。
6. 汲み上げ対象が0件のポンプは警告行を出す。受け入れ基準: `pumpingFluids` が空のとき `pump-no-vein` が表示され、非空のときは表示されない。
7. ポンプの汲み上げ対象は「底面フットプリントとXZで重なる流体鉱脈 ∧ マスタ `generateFluid` に含まれる流体」で決まる（Yは見ない）。受け入れ基準: 1×1×1のテストポンプを水鉱脈のXZ内・Y=7に置いても水が溜まる。3×1×3のフットプリントで原点が鉱脈外・端が鉱脈に掛かるとき `IsPumpableVein` が真。
8. ポンプは汲み上げられる流体鉱脈の上にしか置けない（クライアント側制限のみ、サーバーは弾かない）。受け入れ基準: `VeinPlacementReporter` が鉱脈外セルを `Placeable=false` にし、カーソルセルに `PlacePumpOutsideVein` ツールチップを1行出す。
9. 設置時の鉱脈範囲表示は設置判定と同じ集合（汲み上げられる流体鉱脈だけ）。受け入れ基準: `PlacementVeinViewResolver.Resolve` がポンプ選択時に `generateFluid` に含まれる流体の鉱脈だけを返す。
10. 既存セーブの鉱脈外ポンプはロード時に新規則で引き直す。受け入れ基準: ロード経路（`Load`）も `New` と同じ `ResolveGenerationEntries` を通る（追加のセーブ項目は無い）。
11. `PumpFluidVeinTest` の3ケースが新規則でも通る。

**やらないこと（スコープ境界）:**

- サーバー側の設置拒否（採掘機と同じくクライアント側制限のみ）
- ポンプの動力モデル・生成量・内部タンク容量など挙動の変更
- 新プロトコル・新イベントパケットの追加（既存 `BlockState` イベントに乗せる）
- uGUI側（`MinerBlockInventoryView` 等）への追加。uGUIは完全撤去中（`.decisions/2026-09-05-uGUIはパッケージごと完全撤去する.md`）
- `blockUIAddressablesPath` フィールドの改名・廃止（uGUI撤去側の責務）
- テストMod（`ForUnitTest` / `EditModeInPlayingTestMod`）の `blockUIAddressablesPath` 変更（クライアント側のブロック開閉テストは `TestElectricMachine` を使っており不要）

## Global Constraints

`AGENTS.md` の全規約が全タスクに適用される。特に本planで踏みやすいもの:

- `partial` 禁止。`Func<>` 禁止。try-catch 原則禁止。1ファイル200行以下。1ディレクトリ10ファイル以下（`Game.Block/Blocks/Pump/` は現在4ファイル、+1で5）。
- イベントは UniRx（`Subject<Unit>` を private 保持し `IObservable<Unit>` で公開）。
- コメントは日本語→英語の2行セット、各1行。
- 「汎用基盤にドメイン語彙を持ち込まない」: `Game.Map`（鉱脈層）はポンプを知らない。`IFluidMapVeinDatastore` は `Veins` を公開するだけで、絞り込みは `PumpVeinFootprintJudge` を呼ぶ側が行う（ADR 0039 と同じ構図）。
- 状態変化の検知は購読または操作直後プッシュ。`PumpStateComponent.Update()` は物理進行（tick）に同期した状態配信であり、採掘機 `CheckStateAndInvokeEventUpdate` と同じ前例。
- .cs 変更後は必ず `uloop compile --project-path ./moorestech_client`。テストは `--filter-type regex` で限定。`uloop run-tests` の既定は PlayMode なので EditMode テストは `--test-mode EditMode` を付ける。
- `Localization/localization.csv` を編集したら Web側は `npm run gen:i18n`、C#側は `uloop compile --force-recompile`（生成キーが更新されないと `CS0117` になる）。
- 別リポジトリ（`../moorestech_master`）の変更は push + PR 必須。`.moorestech-external-revisions.json` のピンはそのPRの push 済みコミットを指す。
- 作業は `moores-wt new` で切った使い捨て worktree で行う（`CLAUDE.local.md`）。
- 並列セッションの暫定版（内部タンクを `FluidMachineInventoryStateDetail` で配信する最小実装）がマージ済みなら、Task 2 Step 6 は「同一内容の確認」で終える。

## ファイル構成（責務マップ）

**サーバー（新規）**
- `moorestech_server/Assets/Scripts/Game.Block.Interface/Vein/PumpVeinFootprintJudge.cs` — 汲み上げ対象の合成規則（XZ重なり ∧ generateFluid 一致）。クライアントと共用。
- `moorestech_server/Assets/Scripts/Game.Block.Interface/State/PumpBlockStateDetail.cs` — 汲み上げ中流体と公称生成量の配信DTO（MessagePack）。
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump/PumpStateComponent.cs` — `IBlockStateObservable`。生成中は毎tick、待機へ落ちた瞬間に1回、状態変化を発火し `PumpBlockStateDetail` を返す。

**サーバー（変更）**
- `Game.Map.Interface/Vein/IFluidMapVeinDatastore.cs` / `Game.Map/FluidMapVeinDatastore.cs` — `GetVeinsContainingCell` を `Veins` へ置換。
- `Game.Block/Blocks/Pump/PumpFluidGenerationUtility.cs` — 引数を `(GenerateFluids, BlockPositionInfo)` にし判定を `PumpVeinFootprintJudge` へ委譲。
- `Game.Block/Blocks/Pump/ElectricPumpProcessorComponent.cs` / `Game.Block/Blocks/Gear/GearPumpComponent.cs` — 生成エントリを外から受け取る。
- `Game.Block/Blocks/Pump/ElectricPumpComponent.cs` — `IBlockStateDetail` を実装し `CommonMachineBlockStateDetail` を返す。
- `Game.Block/Blocks/Pump/PumpFluidOutputComponent.cs` — `IBlockStateDetail` を実装し `FluidMachineInventoryStateDetail` を返す（暫定版と同一）。
- `Game.Block/Factory/BlockTemplate/VanillaElectricPumpTemplate.cs` / `VanillaGearPumpTemplate.cs` — エントリ解決とコンポーネント登録。
- `Tests/CombinedTest/Core/PumpFluidVeinTest.cs`、新規 `Tests/UnitTest/Game/PumpVeinFootprintJudgeTest.cs`、新規 `Tests/CombinedTest/Core/PumpBlockStateDetailTest.cs`。

**クライアント（変更）**
- `Client.Game/InGame/Map/MapVein/MapVeinAabb.cs` / `MapVeinAabbRegistry.cs` — 流体鉱脈の `VeinFluidId` を持つ。
- `Client.Game/InGame/BlockSystem/PlaceSystem/Common/VeinPlacementReporter.cs` — ポンプの第3制限。
- `Client.Game/InGame/BlockSystem/PlaceSystem/VeinRestriction/PlacementVeinViewResolver.cs` — ポンプは汲み上げられる流体鉱脈だけ。
- `Localization/localization.csv` — `ui.tooltip.placePumpOutsideVein`、`ui.blockInventory.pumpNoVein`。
- `Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtos.cs` / `BlockInventoryDtos.cs` / `BlockDetailDtoBuilder.cs` — `PumpDetailDto`。
- `Client.Tests/PlaceSystem/VeinPlacementReporterTest.cs` / `PlacementVeinViewResolverTest.cs`。

**Web UI（変更）**
- `src/bridge/contract/schemas/inventory.ts` / `payloadTypes.ts` — `PumpDetailDataSchema`。
- `src/features/blockInventory/details/PumpSection.tsx`（新規）/ `views/SectionStackView.tsx` / `blockInventoryDesign.test.ts`。
- `e2e/mock-host/blockDetailFixtures.ts` / `httpHandler.ts` / `fixtures/blockLocalizationFixtures.ts` / `e2e/tests/block/blockDetails.spec.ts` / `e2e/tests/regression/sectionStack.spec.ts` / `e2e/tests/block/blockRegistryCoverage.spec.ts` / `e2e/fixtures/v8-block-ui-registry.json`。

**マスタ（別repo）**
- `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json` — 油井・歯車ポンプの `blockUIAddressablesPath`。
- `.moorestech-external-revisions.json` — ピン更新。

---

### Task 1: 共有判定 `PumpVeinFootprintJudge` と鉱脈層の `Veins` 公開、サーバー汲み上げ対象のフットプリント化

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Vein/PumpVeinFootprintJudge.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/PumpVeinFootprintJudgeTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Map.Interface/Vein/IFluidMapVeinDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Map/FluidMapVeinDatastore.cs:43-53`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump/PumpFluidGenerationUtility.cs:17-41`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump/ElectricPumpProcessorComponent.cs:16-27`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/GearPumpComponent.cs:14-28`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaElectricPumpTemplate.cs:28-36`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearPumpTemplate.cs:28-42`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/PumpFluidVeinTest.cs`

**Interfaces:**
- Consumes: `BlockPositionInfoExtension.OverlapsVeinXz(this BlockPositionInfo, Vector3Int, Vector3Int)`（既存）、`IFluidMapVein { FluidId VeinFluidId; Vector3Int VeinRangeMin; Vector3Int VeinRangeMax; }`（既存）、`Mooresmaster.Model.GenerateFluidsModule.GenerateFluids`（`.items` は `Element[]`、各 `FluidGuid`/`Amount`/`GenerateTime`）
- Produces:
  - `static HashSet<FluidId> PumpVeinFootprintJudge.ResolvePumpableFluidIds(GenerateFluids generateFluids)`
  - `static bool PumpVeinFootprintJudge.IsPumpableVein(BlockPositionInfo footprint, HashSet<FluidId> pumpableFluidIds, Vector3Int veinMinCell, Vector3Int veinMaxCell, FluidId veinFluidId)`
  - `IReadOnlyList<IFluidMapVein> IFluidMapVeinDatastore.Veins { get; }`
  - `static List<FluidGenerationEntry> PumpFluidGenerationUtility.ResolveGenerationEntries(GenerateFluids generateFluids, BlockPositionInfo footprint)`
  - `ElectricPumpProcessorComponent(ElectricPumpBlockParam param, PumpFluidOutputComponent output, List<FluidGenerationEntry> entries)` と `public IReadOnlyList<FluidGenerationEntry> Entries`（Task 2 が使う）
  - `GearPumpComponent(GearPumpBlockParam param, GearEnergyTransformer gearEnergyTransformer, PumpFluidOutputComponent output, List<FluidGenerationEntry> entries)`

- [x] **Step 1: 判定の単体テストを書く（失敗する）**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/PumpVeinFootprintJudgeTest.cs`:

```csharp
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Vein;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    /// <summary>
    ///     ポンプの汲み上げ対象判定は採掘機と同じ底面XZ重なりで、Yと原点セルは見ない
    ///     The pump target rule is the miner's footprint XZ overlap; neither Y nor the origin cell matters
    /// </summary>
    public class PumpVeinFootprintJudgeTest
    {
        private static readonly FluidId Water = new(1);
        private static readonly FluidId Steam = new(2);
        private static readonly Vector3Int VeinMin = new(10, 0, 10);
        private static readonly Vector3Int VeinMax = new(12, 0, 12);

        [Test]
        public void 原点が鉱脈外でも底面の端が鉱脈にXZで掛かれば対象になる()
        {
            // 3x1x3 北向き原点(8,0,8): x:8..10 z:8..10 で鉱脈角(10,10)に掛かる
            // 3x1x3 facing north at (8,0,8) spans x:8..10 z:8..10 and touches the vein corner (10,10)
            var footprint = new BlockPositionInfo(new Vector3Int(8, 0, 8), BlockDirection.North, new Vector3Int(3, 1, 3));
            var pumpable = new HashSet<FluidId> { Water };

            Assert.IsTrue(PumpVeinFootprintJudge.IsPumpableVein(footprint, pumpable, VeinMin, VeinMax, Water));
        }

        [Test]
        public void 鉱脈AABBのYから外れてもXZが重なれば対象になる()
        {
            var footprint = new BlockPositionInfo(new Vector3Int(11, 7, 11), BlockDirection.North, new Vector3Int(1, 1, 1));
            var pumpable = new HashSet<FluidId> { Water };

            Assert.IsTrue(PumpVeinFootprintJudge.IsPumpableVein(footprint, pumpable, VeinMin, VeinMax, Water));
        }

        [Test]
        public void 隣接だけでは対象にならない()
        {
            var footprint = new BlockPositionInfo(new Vector3Int(13, 0, 10), BlockDirection.North, new Vector3Int(1, 1, 1));
            var pumpable = new HashSet<FluidId> { Water };

            Assert.IsFalse(PumpVeinFootprintJudge.IsPumpableVein(footprint, pumpable, VeinMin, VeinMax, Water));
        }

        [Test]
        public void generateFluidに無い流体の鉱脈は重なっても対象にならない()
        {
            var footprint = new BlockPositionInfo(new Vector3Int(11, 0, 11), BlockDirection.North, new Vector3Int(1, 1, 1));
            var pumpable = new HashSet<FluidId> { Water };

            Assert.IsFalse(PumpVeinFootprintJudge.IsPumpableVein(footprint, pumpable, VeinMin, VeinMax, Steam));
        }
    }
}
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `PumpVeinFootprintJudge` が存在しないためコンパイルエラー（CS0103）

- [x] **Step 3: 判定クラスを書く**

`moorestech_server/Assets/Scripts/Game.Block.Interface/Vein/PumpVeinFootprintJudge.cs`:

```csharp
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.GenerateFluidsModule;
using UnityEngine;

namespace Game.Block.Interface.Vein
{
    /// <summary>
    ///     ポンプが汲み上げられる鉱脈かを決める唯一の実装。クライアントの設置判定とサーバーの汲み上げ対象決定が同じ合成規則を呼ぶ
    ///     The sole implementation deciding whether a pump can draw from a vein; the client placement check and the server target selection call the same composed rule
    ///     位置の規則は BlockPositionInfoExtension.OverlapsVeinXz が正本で、ここはそれと generateFluid 一致を合成するだけ（ADR 0051）
    ///     BlockPositionInfoExtension.OverlapsVeinXz owns the positional rule; this only composes it with the generateFluid match (ADR 0051)
    /// </summary>
    public static class PumpVeinFootprintJudge
    {
        /// <summary>
        ///     汲み上げられる流体IDを先に解決して呼び出し側が持ち回る。設置プレビューは毎フレーム回るためmaster引きをここへ寄せない
        ///     Resolve the pumpable fluid ids once and let the caller hold them; the placement preview runs every frame, so master lookups must not sit in the loop
        /// </summary>
        public static HashSet<FluidId> ResolvePumpableFluidIds(GenerateFluids generateFluids)
        {
            var pumpableFluidIds = new HashSet<FluidId>();
            foreach (var entry in generateFluids.items)
            {
                if (entry.GenerateTime <= 0) continue;
                pumpableFluidIds.Add(MasterHolder.FluidMaster.GetFluidId(entry.FluidGuid));
            }

            return pumpableFluidIds;
        }

        /// <summary>
        ///     generateFluid に無い流体は生成量が定義されないため、一致する鉱脈だけを汲み上げ対象にする
        ///     A fluid absent from generateFluid has no generation amount, so only matching veins are pump targets
        /// </summary>
        public static bool IsPumpableVein(BlockPositionInfo footprint, HashSet<FluidId> pumpableFluidIds, Vector3Int veinMinCell, Vector3Int veinMaxCell, FluidId veinFluidId)
        {
            return pumpableFluidIds.Contains(veinFluidId) && footprint.OverlapsVeinXz(veinMinCell, veinMaxCell);
        }
    }
}
```

- [x] **Step 4: 鉱脈層を `Veins` 公開へ置換する**

`moorestech_server/Assets/Scripts/Game.Map.Interface/Vein/IFluidMapVeinDatastore.cs` 全文:

```csharp
using System.Collections.Generic;

namespace Game.Map.Interface.Vein
{
    public interface IFluidMapVeinDatastore
    {
        // ポンプの判定は鉱脈側では持たず、呼び出し側がPumpVeinFootprintJudgeで絞る（ADR 0051）
        // Pump judgement is not owned by the vein layer; callers filter with PumpVeinFootprintJudge (ADR 0051)
        public IReadOnlyList<IFluidMapVein> Veins { get; }
    }
}
```

`moorestech_server/Assets/Scripts/Game.Map/FluidMapVeinDatastore.cs` の `GetVeinsContainingCell` メソッド（43〜53行目）を削除し、フィールド宣言の直後に次を追加する:

```csharp
        public IReadOnlyList<IFluidMapVein> Veins => _fluidVeins;
```

`using UnityEngine;` は `Debug.LogError` で使っているので残す。

- [x] **Step 5: `PumpFluidGenerationUtility.ResolveGenerationEntries` をフットプリント基準へ変える**

`moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump/PumpFluidGenerationUtility.cs` の `ResolveGenerationEntries` を次で置換する（`using Game.Block.Interface;` と `using Game.Block.Interface.Vein;` を追加、`using UnityEngine;` は不要になるので削除）:

```csharp
        // 底面フットプリントとXZで重なる流体鉱脈のうちマスタgenerateFluidに存在する流体だけをブロック生成時に確定する
        // Resolve, once at block creation, the fluids whose veins overlap the footprint in XZ and exist in the master generateFluid table
        public static List<FluidGenerationEntry> ResolveGenerationEntries(GenerateFluids generateFluids, BlockPositionInfo footprint)
        {
            var entries = new List<FluidGenerationEntry>();
            var pumpableFluidIds = PumpVeinFootprintJudge.ResolvePumpableFluidIds(generateFluids);
            var targetFluidIds = new HashSet<FluidId>();
            foreach (var vein in ServerContext.FluidMapVeinDatastore.Veins)
            {
                if (!PumpVeinFootprintJudge.IsPumpableVein(footprint, pumpableFluidIds, vein.VeinRangeMin, vein.VeinRangeMax, vein.VeinFluidId)) continue;
                targetFluidIds.Add(vein.VeinFluidId);
            }

            // 同一流体は1本にまとめ、公称量はマスタの並び順で決める
            // Each fluid appears once; the nominal rate follows the master ordering
            foreach (var gen in generateFluids.items)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidId(gen.FluidGuid);
                if (!targetFluidIds.Remove(fluidId)) continue;

                var perSecond = gen.Amount / Math.Max(0.0001, gen.GenerateTime);
                entries.Add(new FluidGenerationEntry(fluidId, perSecond));
            }

            return entries;
        }
```

`using Mooresmaster.Model.GenerateFluidsModule;` は既存のまま（`GenerateFluids` 型はこのモジュール）。

- [x] **Step 6: 2つのポンプコンポーネントがエントリを外から受け取る形にする**

`ElectricPumpProcessorComponent.cs` のフィールド・コンストラクタを次で置換する（`using UnityEngine;` は `Mathf` で使うので残す）:

```csharp
        private readonly PumpFluidOutputComponent _output;
        private readonly ElectricPower _requiredPower;
        private readonly List<FluidGenerationEntry> _entries;
        private ElectricPower _currentPower;
        public bool CanGenerateFluid => _entries.Count > 0 && _output.CanAcceptGeneratedFluid;
        public IReadOnlyList<FluidGenerationEntry> Entries => _entries;

        public ElectricPumpProcessorComponent(ElectricPumpBlockParam param, PumpFluidOutputComponent output, List<FluidGenerationEntry> entries)
        {
            _output = output;
            _requiredPower = new ElectricPower(Mathf.Max(0.0001f, param.RequiredPower));
            _entries = entries;
        }
```

`GearPumpComponent.cs` のコンストラクタを次で置換する:

```csharp
        public GearPumpComponent(GearPumpBlockParam param, GearEnergyTransformer gearEnergyTransformer, PumpFluidOutputComponent output, List<FluidGenerationEntry> entries)
        {
            _gearEnergyTransformer = gearEnergyTransformer;
            _output = output;
            _entries = entries;
            _idleTorqueRate = param.GearConsumption.IdlePowerRate;

            UpdateTorqueRequestRate();
        }
```

`GearPumpComponent` に `public bool CanGenerateFluid => _entries.Count > 0 && _output.CanAcceptGeneratedFluid;` を追加し、`UpdateTorqueRequestRate` 内の `var canGenerateFluid = ...` をこのプロパティ参照に置き換える。

- [x] **Step 7: テンプレートでエントリを解決して渡す**

`VanillaElectricPumpTemplate.cs` の `CreatePump` 内、`processorComponent` 生成行を次で置換する:

```csharp
            var generationEntries = PumpFluidGenerationUtility.ResolveGenerationEntries(param.GenerateFluid, blockPositionInfo);
            var processorComponent = new ElectricPumpProcessorComponent(param, outputComponent, generationEntries);
```

`VanillaGearPumpTemplate.cs` の `pumpComponent` 生成行を次で置換する:

```csharp
            var generationEntries = PumpFluidGenerationUtility.ResolveGenerationEntries(param.GenerateFluid, blockPositionInfo);
            var pumpComponent = new GearPumpComponent(param, gearEnergyTransformer, outputComponent, generationEntries);
```

- [x] **Step 8: `PumpFluidVeinTest` にフットプリント規則のケースを足す**

`PumpFluidVeinTest.cs` に次のテストを追加する（`WaterVeinPos` の水鉱脈は ForUnitTest map.json で x:0..10, y:0, z:0）:

```csharp
        // 鉱脈AABBのYから外れてもXZが重なれば汲み上げる（ADR 0051: 採掘機と同じ底面XZ規則）
        // XZ overlap wins even outside the vein's Y range (ADR 0051: the miner's footprint XZ rule)
        [Test]
        public void PumpAboveVeinY_GeneratesFluid()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var pump = PlacePoweredPump(new Vector3Int(5, 7, 0));

            for (var i = 0; i < 10; i++) GameUpdater.RunFrames(1);

            var inventory = pump.GetComponent<PumpFluidOutputComponent>().GetFluidInventory();
            Assert.AreEqual(1, inventory.Count, "Yが鉱脈外でもXZが重なれば汲み上げるはず");
            Assert.AreEqual(MasterHolder.FluidMaster.GetFluidId(WaterFluidGuid), inventory[0].FluidId);
        }
```

- [x] **Step 9: コンパイルしてテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PumpVeinFootprintJudgeTest|PumpFluidVeinTest|ElectricPumpTest|GearPumpTest|IdlePowerRateTest"`
Expected: 全件 PASS（`PumpOutsideFluidVein_GeneratesNothing` は (30,0,0) が全鉱脈のXZ外なので引き続き PASS、`PumpOnMismatchedFluidVein_GeneratesNothing` は蒸気鉱脈の流体が `generateFluid` に無いので PASS）

- [x] **Step 10: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block.Interface/Vein/PumpVeinFootprintJudge.cs moorestech_server/Assets/Scripts/Game.Block.Interface/Vein/PumpVeinFootprintJudge.cs.meta moorestech_server/Assets/Scripts/Tests/UnitTest/Game/PumpVeinFootprintJudgeTest.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Game/PumpVeinFootprintJudgeTest.cs.meta moorestech_server/Assets/Scripts/Game.Map.Interface/Vein/IFluidMapVeinDatastore.cs moorestech_server/Assets/Scripts/Game.Map/FluidMapVeinDatastore.cs moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/GearPumpComponent.cs moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaElectricPumpTemplate.cs moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearPumpTemplate.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/PumpFluidVeinTest.cs
git commit -m "feat(pump): 汲み上げ対象を採掘機と同じ底面XZ重なりで決める PumpVeinFootprintJudge を導入 (ADR 0051)"
```

---

### Task 2: `PumpBlockStateDetail` / `PumpStateComponent` と油井の `CommonMachineBlockStateDetail`、内部タンクの配信

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/State/PumpBlockStateDetail.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump/PumpStateComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/PumpBlockStateDetailTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump/ElectricPumpComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump/PumpFluidOutputComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaElectricPumpTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearPumpTemplate.cs`

**Interfaces:**
- Consumes: Task 1 の `Entries` / `CanGenerateFluid`、既存 `CommonMachineBlockStateDetail(float currentPower, float requestPower, float processingRate, string currentStateType, string previousStateType)`、既存 `FluidMachineInventoryStateDetail(List<FluidMessagePack> inputTanks, List<FluidMessagePack> outputTanks)`、既存 `VanillaMachineBlockStateConst.IdleState / ProcessingState`（`Game.Block.Blocks.Machine`）
- Produces:
  - `PumpBlockStateDetail { const string BlockStateDetailKey = "Pump"; List<PumpingFluidMessagePack> PumpingFluids; }`、`PumpingFluidMessagePack { int FluidId; double AmountPerSecond; }`
  - `PumpStateComponent : IBlockStateObservable, IUpdatableBlockComponent`（Task 5 のクライアントDTOはキー `"Pump"` で受ける）
  - 油井のブロック状態に `CommonMachine` キー（`CurrentStateType` は `"processing"` / `"idle"`）
  - 両ポンプのブロック状態に `FluidMachineInventory` キー（入力0本・出力1本）

- [x] **Step 1: 状態配信のテストを書く（失敗する）**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/PumpBlockStateDetailTest.cs`:

```csharp
using System;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.Block.Blocks.Machine;
using Game.Context;
using Game.EnergySystem;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using UnityEngine;
using static Tests.Util.ElectricNetworkReflectionTestUtil;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    ///     ポンプがUI用の状態（汲み上げ中流体・内部タンク・電力充足）を配信することを検証する（ADR 0051）
    ///     Verifies the pump publishes the UI state: pumping fluids, inner tank, and power satisfaction (ADR 0051)
    /// </summary>
    public class PumpBlockStateDetailTest
    {
        private static readonly Vector3Int WaterVeinPos = new(10, 0, 0);
        private static readonly Vector3Int NoVeinPos = new(30, 0, 0);
        private static readonly Guid WaterFluidGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void 鉱脈上の油井は汲み上げ中流体と公称量と稼働中を配信する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var pump = PlacePoweredPump(WaterVeinPos);
            GameUpdater.UpdateOneTick();

            var state = pump.GetBlockState();
            var pumpDetail = MessagePackSerializer.Deserialize<PumpBlockStateDetail>(state.CurrentStateDetails[PumpBlockStateDetail.BlockStateDetailKey]);
            Assert.AreEqual(1, pumpDetail.PumpingFluids.Count);
            Assert.AreEqual(MasterHolder.FluidMaster.GetFluidId(WaterFluidGuid).AsPrimitive(), pumpDetail.PumpingFluids[0].FluidId);
            // TestElectricPump は amount 10 / generateTime 4 秒
            // TestElectricPump generates amount 10 every 4 seconds
            Assert.AreEqual(2.5, pumpDetail.PumpingFluids[0].AmountPerSecond, 0.0001);

            var common = MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(state.CurrentStateDetails[CommonMachineBlockStateDetail.BlockStateDetailKey]);
            Assert.AreEqual(VanillaMachineBlockStateConst.ProcessingState, common.CurrentStateType);
            Assert.AreEqual(50f, common.RequestPower, 0.001f, "稼働中の実効要求電力は基礎要求電力そのもの");

            var fluid = MessagePackSerializer.Deserialize<FluidMachineInventoryStateDetail>(state.CurrentStateDetails[FluidMachineInventoryStateDetail.BlockStateDetailKey]);
            Assert.AreEqual(0, fluid.InputTanks.Count);
            Assert.AreEqual(1, fluid.OutputTanks.Count);
            Assert.AreEqual(100, fluid.OutputTanks[0].MaxCapacity, 0.001);
        }

        [Test]
        public void 鉱脈外の油井は汲み上げ中流体が空で待機中を配信する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var pump = PlacePoweredPump(NoVeinPos);
            GameUpdater.UpdateOneTick();

            var state = pump.GetBlockState();
            var pumpDetail = MessagePackSerializer.Deserialize<PumpBlockStateDetail>(state.CurrentStateDetails[PumpBlockStateDetail.BlockStateDetailKey]);
            Assert.AreEqual(0, pumpDetail.PumpingFluids.Count);

            var common = MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(state.CurrentStateDetails[CommonMachineBlockStateDetail.BlockStateDetailKey]);
            Assert.AreEqual(VanillaMachineBlockStateConst.IdleState, common.CurrentStateType);
            Assert.AreEqual(50f * 0.2f, common.RequestPower, 0.001f, "待機中の実効要求電力は基礎要求×idlePowerRate");
        }

        [Test]
        public void 生成中は毎tick状態変化を発火し待機へ落ちた直後に1回だけ発火する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var pump = PlacePoweredPump(WaterVeinPos);
            var fired = 0;
            using var subscription = pump.BlockStateChange.Subscribe(_ => fired++);

            // タンク容量100 / 秒2.5 なので40秒で満杯。満杯までは毎tick発火する
            // Capacity 100 at 2.5/s fills in 40 seconds; every tick fires until then
            GameUpdater.RunFrames(10);
            Assert.AreEqual(10, fired, "生成中は毎tick発火するはず");

            for (var i = 0; i < GameUpdater.SecondsToTicks(45); i++) GameUpdater.UpdateOneTick();
            var firedAtFull = fired;
            GameUpdater.RunFrames(5);
            Assert.AreEqual(firedAtFull, fired, "満杯で待機中になった後は発火しないはず");
        }

        private static IBlock PlacePoweredPump(Vector3Int pos)
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPump, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pump);

            var polePosition = pos + new Vector3Int(2, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, polePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(pos, polePosition);

            GameUpdater.UpdateOneTick();
            var networkDatastore = ServerContext.GetService<IElectricWireNetworkLookup>();
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(pump.BlockInstanceId, out var segment));
            AddGenerator(segment, new TestElectricGenerator(new ElectricPower(10000), new BlockInstanceId(10)));
            GameUpdater.UpdateOneTick();

            return pump;
        }
    }
}
```

`IBlock.GetBlockState()` と `IBlock.BlockStateChange`（`IObservable<BlockState>`）は `Game.Block.Interface/IBlock.cs` の実名（`BlockSystem` は `_onBlockStateChange.OnNext(GetBlockState())` で配信している）。`BlockState.CurrentStateDetails` が `Dictionary<string, byte[]>` であることも同ファイル `State/BlockState.cs` で確認する。

- [x] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `PumpBlockStateDetail` 未定義のコンパイルエラー

- [x] **Step 3: `PumpBlockStateDetail` を書く**

`moorestech_server/Assets/Scripts/Game.Block.Interface/State/PumpBlockStateDetail.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using MessagePack;

namespace Game.Block.Interface.State
{
    /// <summary>
    ///     ポンプの汲み上げ中流体と公称生成量。汲み上げ対象が無ければ空リスト（クライアントは鉱脈警告を出す）
    ///     The fluids a pump is drawing and their nominal rates; empty when it has no target (the client shows the vein warning)
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class PumpBlockStateDetail
    {
        public const string BlockStateDetailKey = "Pump";

        [Key(0)] public List<PumpingFluidMessagePack> PumpingFluids;

        public PumpBlockStateDetail(List<PumpingFluidMessagePack> pumpingFluids)
        {
            PumpingFluids = pumpingFluids;
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PumpBlockStateDetail()
        {
        }
    }

    [MessagePackObject]
    public class PumpingFluidMessagePack
    {
        [Key(0)] public int FluidId { get; set; }

        // 充足率100%のときの秒あたり量。分間換算はクライアントが行う
        // Per-second amount at 100% satisfaction; the client converts to per-minute
        [Key(1)] public double AmountPerSecond { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PumpingFluidMessagePack()
        {
        }

        public PumpingFluidMessagePack(FluidId fluidId, double amountPerSecond)
        {
            FluidId = fluidId.AsPrimitive();
            AmountPerSecond = amountPerSecond;
        }
    }
}
```

- [x] **Step 4: `PumpStateComponent` を書く**

`moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump/PumpStateComponent.cs`:

```csharp
using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using MessagePack;
using UniRx;

namespace Game.Block.Blocks.Pump
{
    /// <summary>
    ///     ポンプの状態配信。生成中は毎tick、待機へ落ちた直後に1回発火する（採掘機 CheckStateAndInvokeEventUpdate と同じ節度）
    ///     Publishes pump state: every tick while generating and once on the drop to idle (same cadence as the miner's CheckStateAndInvokeEventUpdate)
    /// </summary>
    public class PumpStateComponent : IBlockStateObservable, IUpdatableBlockComponent
    {
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
        private readonly Subject<Unit> _onChangeBlockState = new();

        private readonly IReadOnlyList<FluidGenerationEntry> _entries;
        private readonly IPumpGenerationState _generationState;
        private bool _wasGenerating;

        public PumpStateComponent(IReadOnlyList<FluidGenerationEntry> entries, IPumpGenerationState generationState)
        {
            _entries = entries;
            _generationState = generationState;
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);

            // 生成中は毎tick、生成→待機の遷移は1回だけ通知する
            // Notify every tick while generating and once on the generating-to-idle transition
            var isGenerating = _generationState.CanGenerateFluid;
            if (isGenerating || _wasGenerating) _onChangeBlockState.OnNext(Unit.Default);
            _wasGenerating = isGenerating;
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            var pumpingFluids = new List<PumpingFluidMessagePack>();
            foreach (var entry in _entries) pumpingFluids.Add(new PumpingFluidMessagePack(entry.FluidId, entry.PerSecond));

            var detail = new PumpBlockStateDetail(pumpingFluids);
            return new[] { new BlockStateDetail(PumpBlockStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail)) };
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
            _onChangeBlockState.Dispose();
        }
    }

    /// <summary>
    ///     電気・歯車の両ポンプが「いま生成できるか」を同じ形で答える
    ///     Both the electric and gear pumps answer "can it generate now" through the same shape
    /// </summary>
    public interface IPumpGenerationState
    {
        bool CanGenerateFluid { get; }
    }
}
```

`ElectricPumpProcessorComponent` と `GearPumpComponent` のクラス宣言に `, IPumpGenerationState` を追加する（両者とも Task 1 で `CanGenerateFluid` を public プロパティとして持つ）。

- [x] **Step 5: 油井の `ElectricPumpComponent` に `CommonMachineBlockStateDetail` を持たせる**

`ElectricPumpComponent.cs` 全文を次で置換する:

```csharp
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.EnergySystem;
using MessagePack;

namespace Game.Block.Blocks.Pump
{
    /// <summary>
    /// 所属セグメントの確定済み供給率から実効電力を導出してポンプProcessorへ渡す
    /// Derives effective power from its segment's settled supply rate and feeds the pump processor
    /// UI向けには実効要求電力と充足率をCommonMachineBlockStateDetailで配信する（ADR 0010 / 0051）
    /// For the UI it publishes the effective request and satisfaction via CommonMachineBlockStateDetail (ADR 0010 / 0051)
    /// </summary>
    public class ElectricPumpComponent : IElectricConsumer, IElectricTickPostHandler, IBlockStateDetail
    {
        public BlockInstanceId BlockInstanceId { get; }
        public ElectricPower RequestEnergy => new(_requestEnergy.AsPrimitive() * (_processor.CanGenerateFluid ? 1f : _idlePowerRate));

        private readonly ElectricPumpProcessorComponent _processor;
        private readonly ElectricPower _requestEnergy;
        private readonly float _idlePowerRate;
        private ElectricPower _suppliedPower;

        public ElectricPumpComponent(BlockInstanceId blockInstanceId, ElectricPower requestEnergy, float idlePowerRate, ElectricPumpProcessorComponent processor)
        {
            BlockInstanceId = blockInstanceId;
            _requestEnergy = requestEnergy;
            _idlePowerRate = idlePowerRate;
            _processor = processor;
        }

        public void OnElectricTickPostProcess(ElectricNetworkStatistics statistics)
        {
            BlockException.CheckDestroy(this);

            // 確定した供給率から実効電力を一度だけProcessorへ渡す
            // Push effective power to the processor once from the settled supply rate
            _suppliedPower = new ElectricPower(RequestEnergy.AsPrimitive() * statistics.PowerRate);
            _processor.SupplyExternalPower(_suppliedPower);
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);

            // 稼働状態は「汲み上げ対象あり ∧ タンクに空きあり」の2値。停止中は無い
            // The state is binary, generating or idle; there is no halted state
            var stateType = _processor.CanGenerateFluid ? VanillaMachineBlockStateConst.ProcessingState : VanillaMachineBlockStateConst.IdleState;
            var detail = new CommonMachineBlockStateDetail(_suppliedPower.AsPrimitive(), RequestEnergy.AsPrimitive(), 0f, stateType, stateType);
            return new[] { new BlockStateDetail(CommonMachineBlockStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail)) };
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
```

`Game.Block/Blocks/Machine/VanillaMachineBlockStateConst.cs` の名前空間が `Game.Block.Blocks.Machine` であることを確認し、違えば `using` を合わせる。

- [x] **Step 6: `PumpFluidOutputComponent` が内部タンクを配信する**

並列セッションの暫定版がマージ済みで `PumpFluidOutputComponent` に既に `IBlockStateDetail` 実装があれば、内容が下記と同じ（入力0本・出力1本・`MaxCapacity` に容量）ことを確認して次へ進む。無ければクラス宣言に `, IBlockStateDetail` を追加し、`using Game.Block.Interface.State;` と `using MessagePack;` を足して次を追加する:

```csharp
        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);

            // ポンプは供給専用なので入力タンク0本、内部タンクを出力タンク1本として配信する
            // A pump is output-only, so it publishes zero input tanks and its inner tank as the single output tank
            var outputTanks = new List<FluidMessagePack> { new(_tank.FluidId, _tank.Amount, _tank.Capacity) };
            var detail = new FluidMachineInventoryStateDetail(new List<FluidMessagePack>(), outputTanks);
            return new[] { new BlockStateDetail(FluidMachineInventoryStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail)) };
        }
```

- [x] **Step 7: テンプレートに `PumpStateComponent` を登録する**

`VanillaElectricPumpTemplate.cs` の `components` リストを次にする（`processorComponent` の後に置く。状態配信は同tickの生成結果を見たいため）:

```csharp
            var stateComponent = new PumpStateComponent(generationEntries, processorComponent);
            var components = new List<IBlockComponent>
            {
                fluidConnector,
                outputComponent,
                electricComponent,
                processorComponent,
                stateComponent,
                wireConnector,
            };
```

`VanillaGearPumpTemplate.cs` も同様に `pumpComponent` の直後へ `new PumpStateComponent(generationEntries, pumpComponent)` を追加する。

- [x] **Step 8: コンパイルしてテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PumpBlockStateDetailTest|PumpFluidVeinTest|ElectricPumpTest|GearPumpTest|IdlePowerRateTest"`
Expected: 全件 PASS

- [x] **Step 9: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block.Interface/State/PumpBlockStateDetail.cs moorestech_server/Assets/Scripts/Game.Block.Interface/State/PumpBlockStateDetail.cs.meta moorestech_server/Assets/Scripts/Game.Block/Blocks/Pump moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/GearPumpComponent.cs moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaElectricPumpTemplate.cs moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearPumpTemplate.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/PumpBlockStateDetailTest.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/PumpBlockStateDetailTest.cs.meta
git commit -m "feat(pump): 汲み上げ中流体・内部タンク・電力充足をブロック状態で配信する PumpStateComponent を追加 (ADR 0051)"
```

---

### Task 3: クライアント設置制限と鉱脈範囲表示をポンプへ拡張する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinAabb.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinAabbRegistry.cs:30-40`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/VeinPlacementReporter.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/VeinRestriction/PlacementVeinViewResolver.cs`
- Modify: `Localization/localization.csv`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/VeinPlacementReporterTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementVeinViewResolverTest.cs`

**Interfaces:**
- Consumes: Task 1 の `PumpVeinFootprintJudge`、既存 `IPumpParam { GenerateFluids GenerateFluid; double InnerTankCapacity; }`（`Mooresmaster.Model.BlocksModule`）、既存 `MapVeinAabbRegistryFixture.Create(params VeinLayoutMessagePack[])`
- Produces:
  - `MapVeinAabb.VeinFluidId : FluidId?`（流体鉱脈のみ非null）、コンストラクタ `MapVeinAabb(Guid veinTypeGuid, Vector3Int minCell, Vector3Int maxCell, MapVeinKind kind, ItemId? veinItemId, FluidId? veinFluidId)`
  - `LocalizationKeys.Ui.Tooltip.PlacePumpOutsideVein`
  - `LocalizationKeys.Ui.BlockInventory.PumpNoVein`（Web側でも `L.ui.blockInventory.pumpNoVein`）

- [x] **Step 1: 設置制限のテストを書く（失敗する）**

`VeinPlacementReporterTest.cs` に次の2テストを追加する。既存の `FluidVeinGuid`（`11111111-0000-0000-0000-000000000002`、ForUnitTest map では水鉱脈）と `FluidVeinCell (20,0,20)` を使う。`ForUnitTestModBlockId.ElectricPump` は水だけを `generateFluid` に持つ。

```csharp
        /// <summary>
        ///     置いた瞬間に何も汲み上げないポンプを作らないため、汲み上げられる流体鉱脈の外は設置不可（ADR 0051）
        ///     A pump that would draw nothing the moment it lands is refused, so cells off a pumpable fluid vein are not placeable (ADR 0051)
        /// </summary>
        [Test]
        public void 鉱脈外のポンプセルをPlaceableFalseにしカーソルセルだけ理由を出す()
        {
            CreateServer();
            var pumpMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricPump);
            var placeInfos = new List<PlaceInfo>
            {
                CreatePlaceInfo(FluidVeinCell, BlockDirection.North),
                CreatePlaceInfo(OutsideVeinCell, BlockDirection.North),
            };
            var feedback = new PlacementFeedback();

            VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(placeInfos, pumpMaster, 1, CreateRegistry(), new VeinRestrictedPlacementState(), feedback);

            Assert.IsTrue(placeInfos[0].Placeable, "a pump over the fluid vein was rejected");
            Assert.IsFalse(placeInfos[1].Placeable, "a pump outside the vein stayed placeable");
            CollectionAssert.AreEqual(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.PlacePumpOutsideVein) }, feedback.Lines);
        }

        /// <summary>
        ///     ポンプはアイテム鉱脈を汲み上げられないので、アイテム鉱脈の上は設置可にしない
        ///     A pump cannot draw from an item vein, so an item vein must not make the cell placeable
        /// </summary>
        [Test]
        public void アイテム鉱脈の上はポンプを設置可にしない()
        {
            CreateServer();
            var pumpMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricPump);
            var placeInfos = new List<PlaceInfo> { CreatePlaceInfo(VeinMinCell, BlockDirection.North) };

            VeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(placeInfos, pumpMaster, -1, CreateRegistry(), new VeinRestrictedPlacementState(), new PlacementFeedback());

            Assert.IsFalse(placeInfos[0].Placeable, "an item vein made a pump placeable");
        }
```

`PlacementVeinViewResolverTest.cs` の既存テスト `ポンプは流体鉱脈だけを出す` の説明コメントを「汲み上げられる流体鉱脈だけを出す」に改め、そのまま維持する（テストの水鉱脈は `generateFluid` に含まれるので期待値は変わらない）。さらに次を追加する。`SteamVeinGuid` は ForUnitTest map の `11111111-0000-0000-0000-000000000003`（`test:SteamVein`、fluidGuid `00000000-0000-0000-1234-000000000002`。`TestElectricPump` の `generateFluid` は水 `...0001` だけなので含まれない）:

```csharp
        private const string SteamVeinGuid = "11111111-0000-0000-0000-000000000003";

        /// <summary>
        ///     表示は設置判定と同じ集合。generateFluidに無い流体の鉱脈は汲み上げられないので出さない（ADR 0051）
        ///     The view shows the same set the placement check uses; veins of fluids absent from generateFluid are not pumpable and stay hidden (ADR 0051)
        /// </summary>
        [Test]
        public void ポンプはgenerateFluidに無い流体の鉱脈を出さない()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var pumpGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricPump).BlockGuid;
            var registry = MapVeinAabbRegistryFixture.Create(
                new VeinLayoutMessagePack(FluidVeinGuid, 20, 0, 20, 20, 0, 20),
                new VeinLayoutMessagePack(SteamVeinGuid, 40, 0, 40, 40, 0, 40));

            var display = PlacementVeinViewResolver.Resolve(registry, new VeinRestrictedPlacementState(), new BlockPlacementTarget(pumpGuid, null));

            CollectionAssert.AreEqual(new[] { Guid.Parse(FluidVeinGuid) }, ToVeinTypeGuids(display));
        }
```

- [x] **Step 2: ローカライズキーを追加する**

`Localization/localization.csv` の `ui.tooltip.placeOutsideTutorialVein` 行の直後に追加:

```
ui.tooltip.placePumpOutsideVein,Place the pump over a fluid vein it can draw from,Place the pump over a fluid vein it can draw from,汲み上げられる流体鉱脈の上に設置してください,Platziere die Pumpe über einer Flüssigkeitsader die sie fördern kann
```

`ui.blockInventory.outputNumber` 行の直後に追加:

```
ui.blockInventory.pumpNoVein,No fluid vein to draw from,No fluid vein to draw from,汲み上げられる鉱脈がありません,Keine Flüssigkeitsader zum Fördern
```

- [x] **Step 3: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client --force-recompile`
Expected: `PlacePumpOutsideVein` は生成済みになり、テストの `ForUnitTestModBlockId.ElectricPump` 等は解決するが、`MarkOutsideVeinCellsAsNotPlaceable` がポンプを素通しするためテスト失敗（コンパイルは通る）

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "VeinPlacementReporterTest|PlacementVeinViewResolverTest"`
Expected: 新規3件が FAIL

- [x] **Step 4: `MapVeinAabb` に流体IDを持たせる**

`MapVeinAabb.cs` のフィールドとコンストラクタを次で置換する:

```csharp
        // アイテム鉱脈の産出アイテム。流体鉱脈はnull
        // The item an item vein yields; null for fluid veins
        public readonly ItemId? VeinItemId;

        // 流体鉱脈の流体。アイテム鉱脈はnull
        // The fluid a fluid vein holds; null for item veins
        public readonly FluidId? VeinFluidId;

        public MapVeinAabb(Guid veinTypeGuid, Vector3Int minCell, Vector3Int maxCell, MapVeinKind kind, ItemId? veinItemId, FluidId? veinFluidId)
        {
            VeinTypeGuid = veinTypeGuid;
            MinCell = minCell;
            MaxCell = maxCell;
            Kind = kind;
            VeinItemId = veinItemId;
            VeinFluidId = veinFluidId;

            // min/maxは内包セル座標なのでmax側に1セル分足してワールドAABBにする
            // min/max are inclusive cell coords, so add one cell on the max side to build the world AABB
            Bounds = new Bounds();
            Bounds.SetMinMax(minCell, maxCell + Vector3Int.one);
        }
```

`MapVeinAabbRegistry.cs` の判別式と生成行を次で置換する:

```csharp
                var (kind, veinItemId, veinFluidId) = element.VeinParam switch
                {
                    ItemVeinParam itemVeinParam => (MapVeinKind.Item, (ItemId?)MasterHolder.ItemMaster.GetItemId(itemVeinParam.ItemGuid), (FluidId?)null),
                    FluidVeinParam fluidVeinParam => (MapVeinKind.Fluid, (ItemId?)null, (FluidId?)MasterHolder.FluidMaster.GetFluidId(fluidVeinParam.FluidGuid)),
                    _ => throw new InvalidOperationException($"[MapVeinAabbRegistry] 未対応のVeinParam:{element.VeinParam.GetType().Name} veinGuid:{veinTypeGuid}"),
                };

                _veins.Add(new MapVeinAabb(veinTypeGuid, minCell, maxCell, kind, veinItemId, veinFluidId));
```

`MapVeinAabb` のコンストラクタを直接呼ぶ箇所を `grep -rn "new MapVeinAabb(" moorestech_client/Assets/Scripts` で全件洗い、第6引数を追加する（テストで直接生成している箇所があれば `(FluidId?)null` を渡す）。

- [x] **Step 5: `VeinPlacementReporter` にポンプの制限を足す**

`VeinPlacementReporter.cs` の `MarkOutsideVeinCellsAsNotPlaceable` を次で置換する（クラスの summary も「3つの設置制限」に改める）:

```csharp
        public static void MarkOutsideVeinCellsAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster, int cursorIndex, MapVeinAabbRegistry veinAabbRegistry, VeinRestrictedPlacementState state, PlacementFeedback feedback)
        {
            // 採掘機・ポンプ・チュートリアル対象のいずれでもないブロックは鉱脈と無関係なので素通しする
            // A block that is neither a miner, a pump nor the tutorial target is unrelated to veins, so let it pass
            var minerParam = holdingBlockMaster.BlockParam as IMinerParam;
            var pumpParam = holdingBlockMaster.BlockParam as IPumpParam;
            var holdingBlockId = MasterHolder.BlockMaster.GetBlockId(holdingBlockMaster.BlockGuid);
            var isRestricted = state.TryGetRestrictedVeinType(holdingBlockId, out var restrictedVeinTypeGuid);
            if (minerParam == null && pumpParam == null && !isRestricted) return;

            // 掘れる/汲み上げられるIDの解決はセル数に依らず1度でよい。設置プレビューは毎フレーム回る
            // Resolving the minable / pumpable ids once is enough regardless of cell count; the placement preview runs every frame
            var minableItemIds = minerParam == null ? null : MinerVeinFootprintJudge.ResolveMinableItemIds(minerParam.MineSettings);
            var pumpableFluidIds = pumpParam == null ? null : PumpVeinFootprintJudge.ResolvePumpableFluidIds(pumpParam.GenerateFluid);

            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                var placeInfo = currentPlaceInfos[i];
                var footprint = new BlockPositionInfo(placeInfo.Position, placeInfo.Direction, holdingBlockMaster.BlockSize);

                // 3つの制限は同じ鉱脈台帳を見るので1度だけ回し、それぞれの重なりを同時に取る
                // All three restrictions read the same vein ledger, so one pass collects every overlap at once
                var isOverMinableVein = false;
                var isOverPumpableVein = false;
                var isOverRestrictedVeinType = false;
                foreach (var vein in veinAabbRegistry.Veins)
                {
                    if (minerParam != null && vein.VeinItemId.HasValue && MinerVeinFootprintJudge.IsMinableVein(footprint, minableItemIds, vein.MinCell, vein.MaxCell, vein.VeinItemId.Value)) isOverMinableVein = true;
                    if (pumpParam != null && vein.VeinFluidId.HasValue && PumpVeinFootprintJudge.IsPumpableVein(footprint, pumpableFluidIds, vein.MinCell, vein.MaxCell, vein.VeinFluidId.Value)) isOverPumpableVein = true;
                    if (isRestricted && vein.VeinTypeGuid == restrictedVeinTypeGuid && footprint.OverlapsVeinXz(vein.MinCell, vein.MaxCell)) isOverRestrictedVeinType = true;
                }

                // 置いた瞬間に何も掘らない採掘機を作らないため、掘れる鉱脈に重ならないセルは不可にする
                // A miner that would mine nothing the moment it lands is refused, so a cell overlapping no minable vein is not placeable
                if (minerParam != null && !isOverMinableVein)
                {
                    placeInfo.Placeable = false;
                    if (i == cursorIndex) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceMinerOutsideVein));
                }

                // 置いた瞬間に何も汲み上げないポンプも同じ理由で不可にする（ADR 0051）
                // A pump that would draw nothing the moment it lands is refused for the same reason (ADR 0051)
                if (pumpParam != null && !isOverPumpableVein)
                {
                    placeInfo.Placeable = false;
                    if (i == cursorIndex) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlacePumpOutsideVein));
                }

                if (isRestricted && !isOverRestrictedVeinType)
                {
                    placeInfo.Placeable = false;
                    if (i == cursorIndex) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceOutsideTutorialVein));
                }
            }
        }
```

- [x] **Step 6: `PlacementVeinViewResolver` のポンプ分岐を汲み上げられる鉱脈に絞る**

`PlacementVeinViewResolver.Resolve` の switch と `#region Internal` を次で置換する:

```csharp
            var blockParam = MasterHolder.BlockMaster.GetBlockMaster(blockTarget.BlockGuid).BlockParam;
            return blockParam switch
            {
                IMinerParam minerParam => VeinDisplay.OfVeins(SelectMinableVeins(minerParam), false),
                IPumpParam pumpParam => VeinDisplay.OfVeins(SelectPumpableVeins(pumpParam), false),
                _ => VeinDisplay.Hidden,
            };

            #region Internal

            // 表示は位置に依らないので掘れるアイテム種別だけで絞る。XZ重なりは設置判定側が同じ鉱脈集合に対して見る
            // The display does not depend on position, so filter by minable item only; the placement check applies the XZ overlap to the same set
            List<MapVeinAabb> SelectMinableVeins(IMinerParam minerParam)
            {
                var minableItemIds = MinerVeinFootprintJudge.ResolveMinableItemIds(minerParam.MineSettings);
                var veins = new List<MapVeinAabb>();
                foreach (var vein in veinAabbRegistry.Veins)
                    if (vein.VeinItemId.HasValue && minableItemIds.Contains(vein.VeinItemId.Value))
                        veins.Add(vein);

                return veins;
            }

            // ポンプも同じ構図。汲み上げられる流体の鉱脈だけを出し、設置判定と同じ集合にする（ADR 0051）
            // Pumps follow the same shape: show only veins of pumpable fluids, the same set the placement check uses (ADR 0051)
            List<MapVeinAabb> SelectPumpableVeins(IPumpParam pumpParam)
            {
                var pumpableFluidIds = PumpVeinFootprintJudge.ResolvePumpableFluidIds(pumpParam.GenerateFluid);
                var veins = new List<MapVeinAabb>();
                foreach (var vein in veinAabbRegistry.Veins)
                    if (vein.VeinFluidId.HasValue && pumpableFluidIds.Contains(vein.VeinFluidId.Value))
                        veins.Add(vein);

                return veins;
            }

            #endregion
```

`MapVeinAabbRegistry.SelectVeinsOfKind` の利用箇所が無くなったら `grep -rn "SelectVeinsOfKind" moorestech_client/Assets/Scripts` で確認し、他に呼び出しが無ければメソッドを削除する（summary の「ポンプのように」も消える）。

- [x] **Step 7: コンパイルしてテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "VeinPlacementReporterTest|PlacementVeinViewResolverTest|PlacementVeinViewPushTest|MapVeinRangeView"`
Expected: 全件 PASS

- [x] **Step 8: コミットする**

```bash
git add Localization/localization.csv moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/VeinPlacementReporter.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/VeinRestriction/PlacementVeinViewResolver.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem
git commit -m "feat(pump): ポンプの設置を汲み上げられる流体鉱脈の上に限定し鉱脈表示を同じ集合にする (ADR 0051)"
```

---

### Task 4: Web UIホストの `PumpDetailDto`

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtos.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockInventoryDtos.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs`

**Interfaces:**
- Consumes: Task 2 の `PumpBlockStateDetail` / `PumpingFluidMessagePack`、既存 `CommonMachineBlockStateDetail`、既存 `GearPumpBlockParam.GearConsumption`
- Produces（Web側 zod スキーマと1対1。JSONキーは既存規約どおり先頭小文字化される）:

```csharp
    public class PumpDetailDto
    {
        // 油井だけ持つ。歯車ポンプは null（キー省略）で Web 側は GearSection を使う
        // Only the electric pump carries this; the gear pump leaves it null (key omitted) and the Web side uses GearSection
        public PumpElectricDto Electric;
        public List<PumpingFluidDto> PumpingFluids;
    }

    public class PumpElectricDto
    {
        public string CurrentState;
        public float CurrentPower;
        public float RequestPower;
    }

    public class PumpingFluidDto
    {
        public int FluidId;
        public string FluidGuid;
        public float AmountPerMinute;
    }
```

- [x] **Step 1: DTO を追加する**

`BlockDetailDtos.cs` の `MiningItemDto` の直後に上記3クラスを追加する。`BlockInventoryDtos.cs` の `BlockInventoryDto` に `public MinerDetailDto Miner;` の直後で `public PumpDetailDto Pump;` を追加する。

- [x] **Step 2: ビルダーに充填処理を追加する**

`BlockDetailDtoBuilder.Apply` の採掘機ブロックの直後（ギアの前）に追加:

```csharp
            // ポンプ: Pump StateDetail。油井は CommonMachine（電力充足）も併せて持つ（ADR 0051）
            // Pumps: the Pump state detail; the electric pump also carries CommonMachine for power satisfaction (ADR 0051)
            var pump = block.GetStateDetail<PumpBlockStateDetail>(PumpBlockStateDetail.BlockStateDetailKey);
            if (pump != null)
            {
                dto.Pump = new PumpDetailDto
                {
                    Electric = common == null ? null : new PumpElectricDto { CurrentState = ToCamelCase(common.CurrentStateType), CurrentPower = common.CurrentPower, RequestPower = common.RequestPower },
                    PumpingFluids = BuildPumpingFluids(pump),
                };
            }
```

`BuildMiningItems` の直後に追加:

```csharp
        private static List<PumpingFluidDto> BuildPumpingFluids(PumpBlockStateDetail pump)
        {
            // 公称量は秒→分に換算し、表示名解決用に FluidGuid を添える（採掘機の ItemsPerMinute と同じ意味）
            // Convert the nominal per-second rate to per-minute and attach the FluidGuid for name resolution (same meaning as the miner's ItemsPerMinute)
            var result = new List<PumpingFluidDto>();
            foreach (var pumping in pump.PumpingFluids)
            {
                var fluidGuid = MasterHolder.FluidMaster.GetFluidMaster(new FluidId(pumping.FluidId)).FluidGuid.ToString("D");
                result.Add(new PumpingFluidDto { FluidId = pumping.FluidId, FluidGuid = fluidGuid, AmountPerMinute = (float)(pumping.AmountPerSecond * 60) });
            }
            return result;
        }
```

`GetGearConsumption` の switch に `GearPumpBlockParam p => p.GearConsumption,` を追加する。

- [x] **Step 3: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [x] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail
git commit -m "feat(webui-host): block_inventory.current にポンプ詳細 PumpDetailDto を載せる (ADR 0051)"
```

---

### Task 5: Web UI `PumpSection` とスキーマ・fixture・テスト

**Files:**
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/inventory.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/payloadTypes.ts`
- Create: `moorestech_web/webui/src/features/blockInventory/details/PumpSection.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/details/PumpSection.test.ts`
- Create: `moorestech_web/webui/src/features/blockInventory/details/pumpSection.module.css`
- Modify: `moorestech_web/webui/src/features/blockInventory/views/SectionStackView.tsx`
- Modify: `moorestech_web/webui/src/features/blockInventory/blockInventoryDesign.test.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures/blockLocalizationFixtures.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/blockDetailFixtures.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/httpHandler.ts`
- Modify: `moorestech_web/webui/e2e/tests/block/blockDetails.spec.ts`
- Modify: `moorestech_web/webui/e2e/tests/regression/sectionStack.spec.ts`
- Modify: `moorestech_web/webui/e2e/tests/block/blockRegistryCoverage.spec.ts`
- Modify: `moorestech_web/webui/e2e/fixtures/v8-block-ui-registry.json`
- Generated: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（`npm run gen:i18n`）

**Interfaces:**
- Consumes: Task 4 の `PumpDetailDto` 形状、Task 3 のキー `ui.blockInventory.pumpNoVein`、既存 `FluidIcon`（`@/shared/ui`、`fluidGuid` でアイコンと名前を解決）、既存 `PowerRateText`、既存 `machineStateDisplay`、既存 `L.ui.blockInventory.itemsPerMinute`
- Produces: testId `pump-section` / `pump-state-label` / `pump-power-rate` / `pump-pumping-fluids` / `pump-no-vein` / `pump-fluid-slots`

- [x] **Step 1: i18n キーを再生成する**

Run: `cd moorestech_web/webui && npm run gen:i18n`
Expected: `src/shared/i18n/generated/localizationKeys.ts` に `pumpNoVein` が増える（`git diff --stat` で1ファイル変更）

- [x] **Step 2: スキーマと型を追加する**

`schemas/inventory.ts` の `MinerDetailDataSchema` の直後に追加:

```ts
export const PumpDetailDataSchema = z.object({
  // 油井だけ electric を持つ。歯車ポンプは省略され GearSection が動力行を担う
  // Only the electric pump carries electric; the gear pump omits it and GearSection renders the power row
  electric: z.object({ currentState: MachineProcessStateSchema, currentPower: z.number(), requestPower: z.number() }).optional(),
  pumpingFluids: z.array(z.object({ fluidId: z.number(), fluidGuid: GuidSchema, amountPerMinute: z.number() })),
});
```

`BlockInventoryOpenSchema` の `miner: MinerDetailDataSchema.optional(),` の直後に `pump: PumpDetailDataSchema.optional(),` を追加する。`MachineProcessStateSchema` と `GuidSchema` が同ファイル内で `PumpDetailDataSchema` より前に宣言されていることを確認する（後ろなら宣言順を入れ替える）。

`payloadTypes.ts` の import リストに `PumpDetailDataSchema,` を追加し、`export type MinerDetailData = ...` の並びに `export type PumpDetailData = z.infer<typeof PumpDetailDataSchema>;` を追加する（`MinerDetailData` の export が無い場合は `BlockInventoryOpen` の直後に置く）。

- [x] **Step 3: `PumpSection` の unit テストを書く（失敗する）**

`moorestech_web/webui/src/features/blockInventory/details/PumpSection.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { pumpSectionDisplay } from "./PumpSection";

// 表示分岐は純関数に切り出し、警告行の出し分けをDOM無しで固定する
// The display branch is a pure function so the warning toggle is pinned without a DOM
describe("pumpSectionDisplay", () => {
  it("汲み上げ中流体が空なら警告行だけを出す", () => {
    const display = pumpSectionDisplay({ pumpingFluids: [] });
    expect(display.showNoVein).toBe(true);
    expect(display.showPumpingFluids).toBe(false);
  });

  it("汲み上げ中流体があれば流体行を出し警告行は出さない", () => {
    const display = pumpSectionDisplay({ pumpingFluids: [{ fluidId: 1, fluidGuid: "54000000-0000-4000-8000-000000000001", amountPerMinute: 150 }] });
    expect(display.showNoVein).toBe(false);
    expect(display.showPumpingFluids).toBe(true);
  });
});
```

Run: `cd moorestech_web/webui && npx vitest run src/features/blockInventory/details/PumpSection.test.ts`
Expected: FAIL（モジュール未存在）

- [x] **Step 4: `PumpSection.tsx` を書く**

```tsx
import { Group, Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen, PumpDetailData } from "@/bridge";
import { FluidIcon } from "@/shared/ui";
import LackHighlightText from "./LackHighlightText";
import PowerRateText from "./PowerRateText";
import { machineStateDisplay } from "./detailLogic";
import { L, useI18n } from "@/shared/i18n";
import styles from "./pumpSection.module.css";

export type PumpSectionDisplay = { showNoVein: boolean; showPumpingFluids: boolean };

// 汲み上げ対象の有無だけで警告行と流体行を排他に出し分ける（ADR 0051）
// Whether the pump has targets alone decides between the warning row and the fluid rows (ADR 0051)
export function pumpSectionDisplay(pump: Pick<PumpDetailData, "pumpingFluids">): PumpSectionDisplay {
  const hasTargets = pump.pumpingFluids.length > 0;
  return { showNoVein: !hasTargets, showPumpingFluids: hasTargets };
}

// ポンプ: 動力行（油井のみ。歯車ポンプは GearSection が担う）+ 公称生成速度 + 鉱脈警告（MinerSection 準拠）
// Pump: power row (electric pump only; GearSection covers the gear pump), nominal rates, and the vein warning (mirrors MinerSection)
export default function PumpSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  if (!data.pump) return null;
  const display = pumpSectionDisplay(data.pump);
  const electric = data.pump.electric;
  const stateDisplay = electric ? machineStateDisplay(electric.currentState) : null;
  return (
    <Stack gap="xs" data-testid="pump-section">
      {electric && stateDisplay ? (
        <>
          <LackHighlightText insufficient={stateDisplay.insufficient} size="sm" testId="pump-state-label">{t(stateDisplay.labelKey)}</LackHighlightText>
          {stateDisplay.showPowerRate && <PowerRateText currentPower={electric.currentPower} requestPower={electric.requestPower} testId="pump-power-rate" />}
        </>
      ) : null}
      {display.showPumpingFluids ? (
        <Group gap="xs" data-testid="pump-pumping-fluids">
          {data.pump.pumpingFluids.map((fluid, i) => (
            <Group key={`${fluid.fluidId}-${i}`} gap={4}>
              <FluidIcon fluidGuid={fluid.fluidGuid} className={styles.icon} />
              <Text size="xs" c="var(--text-default)">
                {t(L.ui.blockInventory.itemsPerMinute, { itemsPerMinute: fluid.amountPerMinute.toFixed(1) })}
              </Text>
            </Group>
          ))}
        </Group>
      ) : null}
      {display.showNoVein ? (
        <LackHighlightText insufficient size="sm" testId="pump-no-vein">{t(L.ui.blockInventory.pumpNoVein)}</LackHighlightText>
      ) : null}
    </Stack>
  );
}
```

`FluidIcon`（`shared/ui/FluidIcon.tsx`、props は `fluidGuid` と `className`）を使う。`FluidSlot` は `FluidSlotData`（量・容量つき）を要求し公称量表示には合わないため使わない。アイコン寸法は同ディレクトリに `pumpSection.module.css` を新設して次の1クラスだけ持つ（`MinerSection` の `ItemSlot` と同じ見た目幅に揃える。値は `shared/ui/ItemSlot/style.module.css` のスロット幅を読んで合わせる）:

```css
.icon {
  width: var(--slot-size, 48px);
  height: var(--slot-size, 48px);
}
```

`--slot-size` が `app/tokens.css` に無ければ `ItemSlot` の実寸をそのまま数値で書く。`MachineProcessState` の型は `data.pump.electric.currentState` が zod 由来で既に一致する。

- [x] **Step 5: `SectionStackView` に組み込む**

`SectionStackView.tsx` に `import PumpSection from "../details/PumpSection";` を追加し、`configByBlockType` に次の2行を追加する:

```ts
  ElectricPump: { itemGridTestId: null, fluidRowTestId: "pump-fluid-slots", renderEmptyGrid: false, showFluidProgress: false },
  GearPump: { itemGridTestId: null, fluidRowTestId: "pump-fluid-slots", renderEmptyGrid: false, showFluidProgress: false },
```

JSX の `<MinerSection data={data} />` の直後に `<PumpSection data={data} />` を追加する。

- [x] **Step 6: デザイン whitelist テストへ登録する**

`blockInventoryDesign.test.ts` の `sources` に `pump: read("./details/PumpSection.tsx"),` を追加する。

- [x] **Step 7: e2e fixture・モック・spec を追加する**

`blockLocalizationFixtures.ts` に定数と名前を追加:

```ts
export const ELECTRIC_PUMP_BLOCK_GUID = "00000000-0000-4000-8000-000000000214";
export const GEAR_PUMP_BLOCK_GUID = "00000000-0000-4000-8000-000000000215";
```

`names` 配列に `[ELECTRIC_PUMP_BLOCK_GUID, "Oil Well", "油井"],` と `[GEAR_PUMP_BLOCK_GUID, "Gear Pump", "歯車ポンプ"],` を追加する。

`blockDetailFixtures.ts` の `blockGearMiner` の直後に追加:

```ts
// BLK-10 油井: pump(electric)/fluidSlots/electricNetwork。汲み上げ中流体あり
// BLK-10 electric pump: pump(electric)/fluidSlots/electricNetwork with an active target
export const blockPump = {
  open: true,
  source: "block",
  blockType: "ElectricPump",
  identifier: "block:10",
  blockGuid: BlockGuids.ELECTRIC_PUMP_BLOCK_GUID,
  itemSlots: [],
  fluidSlots: [{ fluidId: 1, amount: 120, capacity: 200, fluidGuid: WATER_FLUID_GUID }],
  pump: {
    electric: { currentState: "processing", currentPower: 50.0, requestPower: 50.0 },
    pumpingFluids: [{ fluidId: 1, fluidGuid: WATER_FLUID_GUID, amountPerMinute: 3600.0 }],
  },
  electricNetwork: { totalGeneratePower: 100.0, totalRequiredPower: 50.0, consumerCount: 1, powerRate: 1.0 },
} satisfies BlockInventoryWireData;

// BLK-11 鉱脈外の油井: 汲み上げ中流体が空で警告行が出る
// BLK-11 electric pump off a vein: no targets, so the warning row shows
export const blockPumpNoVein = {
  ...blockPump,
  identifier: "block:11",
  fluidSlots: [{ fluidId: 0, amount: 0, capacity: 200, fluidGuid: "" }],
  pump: { electric: { currentState: "idle", currentPower: 10.0, requestPower: 10.0 }, pumpingFluids: [] },
} satisfies BlockInventoryWireData;

// BLK-12 歯車ポンプ: pump(electric無し)/gear/gearNetwork
// BLK-12 gear pump: pump without electric, plus gear/gearNetwork
export const blockGearPump = {
  open: true,
  source: "block",
  blockType: "GearPump",
  identifier: "block:12",
  blockGuid: BlockGuids.GEAR_PUMP_BLOCK_GUID,
  itemSlots: [],
  fluidSlots: [{ fluidId: 1, amount: 30, capacity: 100, fluidGuid: WATER_FLUID_GUID }],
  pump: { pumpingFluids: [{ fluidId: 1, fluidGuid: WATER_FLUID_GUID, amountPerMinute: 120.0 }] },
  gear: { isClockwise: true, currentRpm: 10.0, currentTorque: 2.0, baseRpm: 10.0, baseTorque: 2.0 },
  gearNetwork: { totalRequiredGearPower: 20.0, totalGenerateGearPower: 40.0, stopReason: "none" },
} satisfies BlockInventoryWireData;
```

`httpHandler.ts` の `BLOCK_FIXTURES` に `pump: fx.blockPump, pumpNoVein: fx.blockPumpNoVein, gearPump: fx.blockGearPump,` を追加する。

`blockDetails.spec.ts` の `cases` に `{ type: "pump", testId: "pump-section" },` と `{ type: "gearPump", testId: "pump-section" },` を追加し、末尾に追加:

```ts
test("油井は電力充足率と公称生成速度を出し鉱脈外なら警告行に切り替わる", async ({ page }) => {
  await setBlock(page, "pump");
  await page.goto("/");
  await expect(page.getByTestId("pump-power-rate")).toContainText("100%");
  await expect(page.getByTestId("pump-pumping-fluids")).toContainText("3600.0");
  await expect(page.getByTestId("pump-no-vein")).toHaveCount(0);

  await setBlock(page, "pumpNoVein");
  await page.goto("/");
  await expect(page.getByTestId("pump-no-vein")).toBeVisible();
  await expect(page.getByTestId("pump-pumping-fluids")).toHaveCount(0);
});

test("歯車ポンプは電力行を持たず歯車行を出す", async ({ page }) => {
  await setBlock(page, "gearPump");
  await page.goto("/");
  await expect(page.getByTestId("gear-section")).toBeVisible();
  await expect(page.getByTestId("pump-power-rate")).toHaveCount(0);
});
```

`sectionStack.spec.ts` の `sectionIds` に `"pump-section", "pump-fluid-slots"` を追加し、`cases` に追加:

```ts
  { type: "pump", shown: ["pump-section", "electric-network-section", "pump-fluid-slots"] },
  { type: "gearPump", shown: ["pump-section", "gear-section", "gear-network-section", "pump-fluid-slots"] },
```

`blockRegistryCoverage.spec.ts` の `intentionalGeneric` に `"ElectricPump",` と `"GearPump",` を追加する。`e2e/fixtures/v8-block-ui-registry.json` の `ElectricPump` と `GearPump` の `blockUIAddressablesPath` を `"Vanilla/UI/Block/MachineBlockInventory"` にする（Task 6 のマスタ変更と同じ値）。

- [x] **Step 8: unit テストと e2e を実行する**

Run: `cd moorestech_web/webui && npm run test`
Expected: 全件 PASS（`PumpSection.test.ts`、`blockInventoryDesign.test.ts`、`blockRegistryCoverage` は vitest 対象外なら e2e で）

Run: `cd moorestech_web/webui && npm run test:e2e -- e2e/tests/block/blockDetails.spec.ts e2e/tests/regression/sectionStack.spec.ts e2e/tests/block/blockRegistryCoverage.spec.ts`
Expected: 全件 PASS（e2e はポート 5273 を共有するため、他セッションの e2e と同時実行しない）

- [x] **Step 9: コミットする**

```bash
git add moorestech_web/webui/src/bridge/contract moorestech_web/webui/src/features/blockInventory moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts moorestech_web/webui/e2e
git commit -m "feat(webui): ポンプUI PumpSection（電力充足・公称生成速度・鉱脈警告）を汎用セクション合成に追加 (ADR 0051)"
```

---

### Task 6: マスタデータで油井・歯車ポンプを開けるようにし、ピンを更新する

**Files:**
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`（油井 `019ee389-bf73-73ef-979c-ca7e25697558`、歯車ポンプ `46725c68-888b-4346-a650-db7738cbc9e1`）
- Modify: `.moorestech-external-revisions.json`

**Interfaces:**
- Consumes: クライアント `BlockMasterElementExtension.IsBlockOpenable()`（`blockUIAddressablesPath` 非空で開ける）
- Produces: 油井・歯車ポンプの `blockUIAddressablesPath = "Vanilla/UI/Block/MachineBlockInventory"`

- [ ] **Step 1: 前提を確認する（uGUIプレハブの実ロードが残っていないこと）**

Run: `grep -n "AddressableLoader.LoadAsync" moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventoryState.cs`
Expected: 出力なし（uGUI完全撤去でプレハブのロード・Instantiate が消えている）

出力がある場合は **このタスクを進めずに停止し**、uGUI撤去の進捗と「`MachineBlockInventoryView.Initialize` が `IMachineParam` 前提でポンプでは NRE になる」事実をユーザーへ報告する（現行の uGUI 経路が残った状態でこの値を入れると、Web UI の open 通知が `LoadInventory` の例外で届かず開けない）。

- [ ] **Step 2: マスタを変更して push・PR を出す**

```bash
cd ../moorestech_master
git checkout -b feature/pump-ui-openable master
python3 - <<'EOF'
import json
p='server_v8/mods/moorestechAlphaMod_8/master/blocks.json'
d=json.load(open(p,encoding='utf-8'))
targets={'019ee389-bf73-73ef-979c-ca7e25697558','46725c68-888b-4346-a650-db7738cbc9e1'}
for b in d['data']:
    if b['blockGuid'] in targets:
        b['blockUIAddressablesPath']='Vanilla/UI/Block/MachineBlockInventory'
json.dump(d,open(p,'w',encoding='utf-8'),ensure_ascii=False,indent=2)
open(p,'a',encoding='utf-8').write('\n')
EOF
git diff --stat
git add server_v8/mods/moorestechAlphaMod_8/master/blocks.json
git commit -m "data(v8): 油井・歯車ポンプを開けるようにする（blockUIAddressablesPath、moorestech ADR 0051）"
git push -u origin feature/pump-ui-openable
gh pr create --title "data(v8): 油井・歯車ポンプのUIを開けるようにする (moorestech ADR 0051)" --body "moorestech ADR 0051 のポンプUI対応。blockUIAddressablesPath を機械と同じ値にして IsBlockOpenable を真にする。"
```

`git diff --stat` で変更が blocks.json の2要素分（インデントや改行の全体差分が出ていない）であることを確認する。既存ファイルのインデントが2でない場合は `indent` を既存に合わせる。

- [ ] **Step 3: ピンを更新する**

`.moorestech-external-revisions.json` の `moorestech_master.commitHash` を Step 2 で push したコミットの SHA（`git -C ../moorestech_master rev-parse HEAD`）にする。

- [ ] **Step 4: 実機で開けることを確認する**

worktree の Unity Editor で `uloop compile` 後、unity-playmode-recorded-playtest スキルのプレイテストDSLで「油井を流体鉱脈上に設置 → インタラクト → Web UI の `pump-section` が出る」を1本流す（シナリオ記述はスキルの references を参照）。鉱脈外へのゴースト表示で赤（設置不可）になることも同じ録画で確認する。

- [ ] **Step 5: コミットする**

```bash
git add .moorestech-external-revisions.json
git commit -m "chore: moorestech_master ピンを油井・歯車ポンプUI対応コミットへ更新 (ADR 0051)"
```

---

### Task 7: 全ブランチレビュー（必須・省略不可）

- [ ] **Step 1: moores-code-review を実行する**

必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。`moores-code-review` スキルを起動し、指摘の機械的修正を適用、設計判断は AskUserQuestion で仰ぐ。

- [ ] **Step 2: 最終テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Pump|VeinPlacementReporterTest|PlacementVeinViewResolverTest|IdlePowerRateTest|MinerMiningTest"`
Run: `cd moorestech_web/webui && npm run test && npm run test:e2e`
Expected: 全件 PASS

- [ ] **Step 3: PR を作成する**

pr-create スキルで master 向け PR を作る。本文に ADR 0051・moorestech_master 側 PR のリンク・ピン更新を明記する。PR 作成後は `moores-wt rm <name>` で worktree と Editor を畳む。

---

## 判断記録（ADR）

設計セッションの正本: `docs/adr/0051-pump-ui-and-vein-footprint-parity-with-miner.md`（裁定3件は `.decisions/2026-09-05-油井UIはタンク電力生成速度鉱脈状態を表示する.md` / `2026-09-05-ポンプの鉱脈判定は採掘機と同規則にし設置も鉱脈上限定にする.md` / `2026-09-05-歯車ポンプも油井と同じポンプUI設計に含める.md`）。

planning 中に生じた判断:

1. **`IFluidMapVeinDatastore.GetVeinsContainingCell` は削除し `Veins` に置換する。** 利用者はポンプ生成ユーティリティだけで、流体鉱脈に手掘り用途は無い（`VeinHandMiningService` はアイテム鉱脈のみ）。ADR 0051 の「手掘り用途にのみ残す」はアイテム側 `IItemMapVeinDatastore` に対する記述。出所: agent前提（`IItemMapVeinDatastore.Veins` と同形にして鉱脈層からブロック語彙を消す。ADR 0039 と同じ構図）
2. **生成エントリの解決をテンプレートへ引き上げ、`PumpStateComponent` と処理コンポーネントが同じ `List<FluidGenerationEntry>` を共有する。** 処理コンポーネント内で解決したままだと状態配信側が二重解決するか処理側へ getter を足すことになる。出所: agent前提（採掘機の `SetMiningItem` はコンポーネント内解決だが、採掘機は状態配信も同コンポーネントが担うため二重化しない。ポンプは電気/歯車で処理側が2種あるので共有化した）
3. **「いま生成できるか」は `IPumpGenerationState` の1メソッドで抽象化し、電気/歯車の具象がプッシュではなくプロパティで答える。** `PumpStateComponent` は毎tick `Update()` でこれを読むが、これは購読ではなく tick 同期の状態配信（採掘機 `CheckStateAndInvokeEventUpdate` と同じ役割）。`Func<bool>` は規約で禁止のため interface にした。出所: agent前提
4. **油井の `CommonMachineBlockStateDetail` は `ElectricPumpComponent`（消費者）が返す。** 実効要求電力 `RequestEnergy` と供給率を持つのがこのクラスだけ。`ProcessingRate` は 0 固定（ポンプに工程進捗は無く、Web側もポンプでは `progress` を描かない）。出所: agent前提（ADR 0010 の「実効要求電力を state に詰める」に従う）
5. **歯車ポンプの DTO は `electric` を持たない（optional）。** Web 側は `pump.electric` の有無で動力行を出し分け、歯車の動力行は既存 `GearSection` に委ねる。`PumpDetailDto` に `CurrentState` を歯車でも埋める案は、歯車の稼働状態語彙が既存 UI に無いため採らない。出所: agent前提（ユーザー裁定「電力行を歯車行に差し替え」の実装形）
6. **公称生成速度の表示は `ui.blockInventory.itemsPerMinute`（`{itemsPerMinute}/min`）を流用する。** 単位が分間量である点は同じで、新キーを増やさない。出所: agent前提
7. **マスタの `blockUIAddressablesPath` 値は `Vanilla/UI/Block/MachineBlockInventory`（機械と同じ）。ただし uGUI 完全撤去（`SubInventoryState` のプレハブロード削除）が先。** 現行の `MachineBlockInventoryView.Initialize` は `IMachineParam` 前提でポンプでは NRE になり、Web UI の open 通知が届かなくなる。Task 6 Step 1 で機械的に確認し、残っていれば停止して報告する。出所: agent前提（ADR 0051 agent前提7 の具体化。並列セッションの暫定版がこれと別の値を選んでいた場合は本 plan の値へ揃える）
8. **テストModの `blockUIAddressablesPath` は変更しない。** クライアントの開閉テストは `TestElectricMachine` を使い、ポンプ固有の開閉テストは Task 6 Step 4 の実機確認で代替する。出所: agent前提
9. **`MapVeinAabbRegistry.SelectVeinsOfKind` は利用者が無くなれば削除する。** ポンプ表示が汲み上げ対象だけに絞られ、種別まるごとを見る呼び出しが消えるため。出所: agent前提

## 配置と前例

| 項目 | 配置 | 前例 |
|---|---|---|
| `PumpVeinFootprintJudge` | `Game.Block.Interface/Vein`（クライアント・サーバー共用の static util） | `MinerVeinFootprintJudge`（同ディレクトリ、ADR 0039） |
| `IFluidMapVeinDatastore.Veins` | `Game.Map.Interface/Vein` | `IItemMapVeinDatastore.Veins` |
| `PumpBlockStateDetail` | `Game.Block.Interface/State` | `CommonMinerBlockStateDetail`（同ディレクトリ） |
| `PumpStateComponent` | `Game.Block/Blocks/Pump` | `VanillaMinerProcessorComponent` の `IBlockStateObservable` 実装と発火節度 |
| `IPumpGenerationState` | `PumpStateComponent.cs` 内（同ファイル、ドメイン内 interface） | `IPumpParam` を介した多態（`PlacementVeinViewResolver`） |
| クライアント設置制限 | `VeinPlacementReporter` の第3分岐 | 同クラスの採掘機分岐 |
| Web UI ホスト DTO | `BlockDetailDtos` / `BlockDetailDtoBuilder` | `MinerDetailDto` / `BuildMiningItems` |
| Web UI セクション | `features/blockInventory/details/PumpSection.tsx` + `SectionStackView` 設定 | `MinerSection.tsx` + `configByBlockType.ElectricMiner` |
| ローカライズ | `Localization/localization.csv` → `npm run gen:i18n` / `--force-recompile` | `ui.tooltip.placeMinerOutsideVein`、`ui.blockInventory.machineStateIdle` |
