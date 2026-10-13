# 回転発電機UI Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 回転発電機（blockType `GearToElectricGenerator`）を開けるようにし、汎用ブロックパネルに「発電状態」行と「供給電力 現在 / 最大」行を足す。

**Architecture:** サーバーの `GearToElectricGeneratorComponent` が発電状態（発電中／待機中／停止中）と供給電力を判定し、既存の状態詳細 `GearToElectricGeneratorBlockStateDetail` に載せて、状態変化イベントで送る。クライアントの `Client.WebUiHost` は、それをマスタの最大発電量と合わせて `BlockInventoryDto.GearToElectric` に詰める。WebUI は汎用ビュー `SectionStackView` に新しいセクション `GearToElectricSection` を差し込む。歯車行・歯車網・電力網の各セクションは既存のものがそのまま出る。

**Tech Stack:** Unity C#（サーバー・クライアント、MessagePack、UniRx）、WebUI（React + Mantine + zod、vitest）、マスタJSON（moorestech_master）

## Requirements

1. 回転発電機（v8マスタ `blockGuid 019e158c-df55-7001-be7c-eca97046ca41`）を F で開ける。受け入れ基準: マスタの `openable` が true。PlayModeで開くとブロックパネルが出る。
2. パネルは汎用ビュー（`SectionStackView`）で描く。上から順に、状態行・供給電力行・歯車行（消費トルク／RPM 現在 / 基準）・電力網セクション・歯車網セクション。受け入れ基準: 専用ビューを新設しない。`blockComponentRegistry` に登録しない。
3. 状態行は「発電中」「待機中（需要なし）」「停止中（RPM不足）」の3値。判定は次の順に行う。
   - 現在RPMが0以下、またはマスタ `gearConsumption.minimumRpm` 未満なら停止中。
   - そうでなく、直近の電力tickで電力網へ渡した電力が0より大きければ発電中。
   - それ以外は待機中。
   受け入れ基準: サーバーのテストで3値の判定を固定する。
4. RPM行は赤にしない（用語集「基準RPM」の取り決めどおり）。受け入れ基準: 既存の `GearSection` を変更しない。
5. 供給電力行は「供給電力 {current} / 最大 {max}」。current はこの1台が直近の電力tickで実際に電力網へ渡した電力。max はマスタ `maxGeneratedPower`。電力網セクションの「発電 {generated}」の文言は変えない。受け入れ基準: WebUIのテストで表示文字列を固定する。
6. 電線が繋がっていない回転発電機も、需要0の電力tickを毎tick受けて「供給電力0・待機中」と表示される（孤立した発電機も1つのセグメントになる既存の仕組みどおり）。需要が消えた次のtickで、前回の供給電力を表示に残さない。受け入れ基準: サーバーの結合テストで固定する。
7. 発電状態か供給電力が変わったら、変化を起こす電力tickの後処理の中でブロック状態変化イベントを発火し、クライアントへ届ける。`Update()` では比較も通知もしない。受け入れ基準: サーバーのテストで、供給電力・状態の変化でイベントが発火し、同じ値では発火しないことを固定する。

**やらないこと:**
- 手動の開始・停止操作（ON/OFFスイッチ）
- 内部バッテリー（最大出力1tick分の変換バッファ）の残量・充電率の表示
- 専用ビューの新設
- 他の発電機（ガソリンエンジン発電機など）のUI
- セーブ形式の変更（発電状態は導出値で、セーブしない）

## Global Constraints

- 1ファイル200行未満。1ディレクトリの新規ファイルは10まで。partial・`Func<>`・デフォルト引数・try-catch は使わない（AGENTS.md）
- イベントはUniRx（`Subject`/`IObservable<Unit>`）で通知する。`Action` で発火しない
- 状態変化の検知は、変化を起こす操作（電力tickの後処理での放電）の直後にプッシュする。`Update()` の中で毎tick同じ値かどうかを比べない
- サーバーの時間計測はティック（`GameUpdater`）のみを使う
- コメントは「日本語1行 → 英語1行」の2行セット
- `.meta` は手で作らない（Unityが生成したものはコミットしてよい）
- 翻訳文言の正本は `Localization/localization.csv`（列: `key,Source,english,japanese,german,korean`）。キー型は `pnpm --dir moorestech_web/webui gen:i18n` で再生成する
- マスタ変更は moorestech_master 側のPRで行い、本repoの `.moorestech-external-revisions.json` の `moorestech_master.commitHash` を、そのPRのpush済みコミットへ向ける
- `.cs` を変えたら必ず `uloop compile --project-path ./moorestech_client` を通す

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反1件・修正1件 / `IBlockStateObservable` を再宣言しないと `new` の `OnChangeBlockState` がインターフェース経由で使われない点を、前例（ElectricToGear）に合わせて修正した
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強2・弱4・第3バケツ1 / 強2件は前例どおり据え置き、弱は(b)(d)を採り導出を電力tickの1か所へ寄せた（いずれもシミュレーター予測・確信高の前提宣言）

---

## 配置と前例

| # | 項目 | 配置先 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | `GearToElectricGenerationState` enum（新規） | Game.Block `Blocks/GearToElectric/` | MessagePack で enum 値を送る | `GearStateDetail.Role`（enum `GearRole` を送っている） |
| 2 | `GearToElectricGenerationTracker`（新規。電力tickの後処理で確定・通知） | Game.Block `Blocks/GearToElectric/` | UniRx `Subject<Unit>` で差分通知 | `SimpleGearService.NotifyStateChanged`（前回値との差分のときだけ発火） |
| 3 | `GearToElectricGeneratorComponent` の変更 | 同上 | 基底の `OnChangeBlockState` と Tracker の通知を `Merge` し、`new` で公開し、`IBlockStateObservable` を再宣言 | `ElectricToGearGeneratorComponent:44,73`（基底の通知を自前の通知へ合流させている。前例は Subject への転送で、ここは購読保持の要らない `Merge` にする） |
| 4b | （Phase 2.6）状態詳細はTrackerが電力tickで確定した `State`/`SuppliedPower` を読むだけ | 同上 | 導出1か所 | `SimpleGearService`（通知した値と送る値を同じ時点で揃える） |
| 4 | `GearToElectricGeneratorBlockStateDetail` に `[Key(10)] GenerationState` を追加し、表示しない `ChargeRate`/`BatteryRemaining` を削除 | 同上 | 状態詳細 | 既存の `[Key(7..9)]` と同じ形 |
| 5 | `GearToElectricDetailDtoBuilder`（新規） | Client.WebUiHost `Topics/BlockDetail/` | 状態詳細＋マスタparam → DTO | `ElectricToGearDetailDtoBuilder.cs` |
| 6 | `GearToElectricDetailDto` と `BlockInventoryDto.GearToElectric` | 同上 `BlockDetailDtos.cs` / `BlockInventoryDtos.cs` | DTO | `ElectricToGearDetailDto` |
| 7 | zod schema `GearToElectricDataSchema` | webui `bridge/contract/schemas/energyConversion.ts`（新規。ElectricToGear系も移す） | 契約 | `ElectricToGearDataSchema` |
| 8 | `gearToElectricStateDisplay` 表 | webui `details/detailLogic.ts` | 状態 → 表示を1つの表で決める | `machineStateDisplay` |
| 9 | `GearToElectricSection.tsx`（新規） | webui `details/` | セクション部品 | `GeneratorSection.tsx` / `MachineStateRow.tsx` |
| 10 | `SectionStackView` への差し込みと、`GearToElectricGenerator` 用の設定行 | webui `views/SectionStackView.tsx` | データがあれば描く合成 | 既存の各 `*Section` |

データフロー（読み手が1つ増えるだけで、交差点は作らない）:
`歯車tick/電力tick → GearToElectricGeneratorComponent（書き手）→ [状態詳細 + 状態変化イベント] → BlockInventoryTopic → BlockDetailDtoBuilder → block_inventory.current → SectionStackView（読み手）`

機能パリティ（死活表）: 既存の操作には触れない。新しく開けるようになるだけ。歯車チェーン・電線の接続ツールでブロックを照準する操作は、`openable` の変更で影響を受けない（電柱・回転生成機など、`openable:true` の歯車・電力ブロックがすでに同じ扱いで動いている）。

---

### Task 1: サーバー — 発電状態の判定と通知

前提（実測ではなくコード読み）: 回転発電機のテンプレート（`moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/Gear/VanillaGearToElectricGeneratorTemplate.cs:35`）は常に電線端点を作る。`ElectricWireTopologyMap.cs:37-48` は孤立した1頂点も1セグメントにするので、電線が0本でも電力tickの後処理（`OnElectricTickPostProcess`）は毎tick呼ばれる。`Server.Boot/MasterTickUpdater.cs:51-68` では、電力tick → 歯車tick → ブロック更新の順に回る。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/GearToElectric/GearToElectricGenerationState.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/GearToElectric/GearToElectricGenerationTracker.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/GearToElectric/GearToElectricGeneratorComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/GearToElectric/GearToElectricGeneratorBlockStateDetail.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/GearToElectricGenerationTrackerTest.cs`（新規）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Gear/GearToElectricGenerationStateTest.cs`（新規）

**Interfaces:**
- Produces:
  - `public enum GearToElectricGenerationState { Generating, Idle, Stopped }`（namespace `Game.Block.Blocks.GearToElectric`）
  - `GearToElectricGeneratorBlockStateDetail` のコンストラクタ: `(bool isClockwise, RPM currentRpm, Torque currentTorque, ElectricPower suppliedPower, GearToElectricGenerationState generationState)`。フィールドは `[Key(8)] GeneratedElectricPower`（既存。直近の電力tickでこの1台が電力網へ渡した電力）と `[Key(10)] GenerationState`（新規）。表示に使わない `ChargeRate`（Key 7）・`BatteryRemaining`（Key 9）は削除する
  - `GearToElectricGeneratorComponent.OnChangeBlockState`（`public new IObservable<Unit>`）。歯車の可視値の変化と、発電の可視値（状態・供給電力）の変化の両方で発火する

- [ ] **Step 1: enum を作る**

```csharp
namespace Game.Block.Blocks.GearToElectric
{
    // 回転発電機の発電状態（用語集「発電状態」）
    // Generation state of the rotary generator (glossary "発電状態")
    public enum GearToElectricGenerationState
    {
        Generating,
        Idle,
        Stopped,
    }
}
```

- [ ] **Step 2: Tracker を作る**

```csharp
using System;
using Game.Gear.Common;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.GearToElectric
{
    /// <summary>
    /// 回転発電機の供給電力と発電状態を電力tickごとに確定し、変わったときだけ通知する
    /// Settles the rotary generator's supplied power and generation state per electric tick, notifying only on change
    /// </summary>
    public class GearToElectricGenerationTracker
    {
        public IObservable<Unit> OnGenerationChanged => _onGenerationChanged;

        // 直近の電力tickで確定した値。状態詳細はこれだけを読み、通知した値と送る値を一致させる
        // Values settled at the latest electric tick; the state detail reads only these so notified and sent values match
        public float SuppliedPower { get; private set; }
        public GearToElectricGenerationState State { get; private set; } = GearToElectricGenerationState.Stopped;

        private readonly Subject<Unit> _onGenerationChanged = new();
        private readonly float _minimumRpm;
        private bool _hasNotifiedOnce;

        public GearToElectricGenerationTracker(float minimumRpm)
        {
            _minimumRpm = minimumRpm;
        }

        // 電力tick後処理（放電の直後）に呼ぶ。渡した電力と現在RPMから状態を確定し、差分があれば通知する
        // Called in the electric post-tick right after discharging; settles the state from the handed-over power and current RPM, notifying on change
        public void RecordElectricTick(RPM currentRpm, float suppliedPower)
        {
            var state = ResolveState(currentRpm, suppliedPower);
            if (_hasNotifiedOnce && state == State && Mathf.Approximately(SuppliedPower, suppliedPower)) return;
            _hasNotifiedOnce = true;
            State = state;
            SuppliedPower = suppliedPower;
            _onGenerationChanged.OnNext(Unit.Default);
        }

        // RPM不足が最優先。回っていれば渡した電力の有無で発電中/待機中を分ける
        // RPM shortage wins; when spinning, whether power was handed over splits generating from idle
        private GearToElectricGenerationState ResolveState(RPM currentRpm, float suppliedPower)
        {
            var rpm = currentRpm.AsPrimitive();
            if (rpm <= 0f || rpm < _minimumRpm) return GearToElectricGenerationState.Stopped;
            return suppliedPower > 0f ? GearToElectricGenerationState.Generating : GearToElectricGenerationState.Idle;
        }
    }
}
```

- [ ] **Step 3: コンポーネントを書き換える**

`GearToElectricGeneratorComponent.cs` の変更点（ほかの処理は変えない）:
1. クラス宣言のインターフェース列の末尾に `IBlockStateObservable` を追加する。`new` で公開した `OnChangeBlockState` をインターフェース経由（`BlockSystem` の購読）で使わせるため（前例 `ElectricToGearGeneratorComponent:22` も再宣言している）。`using Game.Block.Interface.Component;` と `using UniRx;` が無ければ足す。
2. フィールド `_lastDischargedPower` を削除し、次を置く:
```csharp
        public new IObservable<Unit> OnChangeBlockState { get; }

        private readonly GearToElectricGenerationTracker _generationTracker;
```
3. 1つ目のコンストラクタの本体:
```csharp
            _param = param;
            _batteryRemaining = 0f;
            _generationTracker = new GearToElectricGenerationTracker((float)param.GearConsumption.MinimumRpm);

            // 歯車の可視値と発電の可視値のどちらが変わってもクライアントへ届ける
            // Deliver to the client when either the gear-visible or the generation-visible values change
            OnChangeBlockState = base.OnChangeBlockState.Merge(_generationTracker.OnGenerationChanged);
```
（前例の `ElectricToGearGeneratorComponent:73` は `Subject` へ `Subscribe` で転送している。ここでは購読を保持する `IDisposable` フィールドが要らず、意味も同じなので `Merge` にする）
4. `DischargeByUtilization` の中で `_lastDischargedPower` を使っている箇所を次に置き換える:
```csharp
                if (statistics.TotalGeneratePower <= 0f)
                {
                    _generationTracker.RecordElectricTick(CurrentRpm, 0f);
                    return;
                }
                var deliveredPower = statistics.TotalRequiredPower * statistics.PowerRate;
                var utilization = Mathf.Clamp01(deliveredPower / statistics.TotalGeneratePower);
                var discharged = _batteryRemaining * utilization;
                _batteryRemaining -= discharged;
                _generationTracker.RecordElectricTick(CurrentRpm, discharged);
```
5. `Update()` の「電線未接続で電力tick後処理が呼ばれない場合でも…」の2行コメントは、事実と合わないので次に直す（処理は変えない）:
```csharp
            // 充電直後の不足分で要求トルクを更新し、満充電なら次の歯車tickで網へ負荷をかけない
            // Refresh the torque request from the post-charge deficit so a full battery stops loading the gear network on the next gear tick
```
6. `CreateDetail()` を次にする（`chargeRate` の計算を消す）:
```csharp
            BlockStateDetail CreateDetail()
            {
                var detail = new GearToElectricGeneratorBlockStateDetail(
                    IsCurrentClockwise,
                    CurrentRpm,
                    CurrentTorque,
                    new ElectricPower(_generationTracker.SuppliedPower),
                    _generationTracker.State);
                var serialized = MessagePackSerializer.Serialize(detail);
                return new BlockStateDetail(GearToElectricGeneratorBlockStateDetail.GearGeneratorBlockStateDetailKey, serialized);
            }
```
変更後のファイルが200行未満であることを `wc -l` で確かめる。

- [ ] **Step 4: 状態詳細を書き換える**

`GearToElectricGeneratorBlockStateDetail.cs` のフィールドとコンストラクタを次にする（`ChargeRate`・`BatteryRemaining` を削除。クライアント・テストとも利用0件を確認済み。`ElectricTickUnificationTest` が読む `BatteryRemaining` は電気→歯車変換機側の別クラス）:
```csharp
        // 直近の電力tickでこの1台が電力網へ渡した電力（供給電力）
        // Power this unit handed to the network on the latest electric tick (supplied power)
        [Key(8)] public float GeneratedElectricPower { get; set; }

        // 発電状態（発電中/待機中/停止中）
        // Generation state (generating / idle / stopped)
        [Key(10)] public GearToElectricGenerationState GenerationState { get; set; }

        public GearToElectricGeneratorBlockStateDetail(
            bool isClockwise,
            RPM currentRpm,
            Torque currentTorque,
            ElectricPower suppliedPower,
            GearToElectricGenerationState generationState) :
            base(isClockwise, currentRpm.AsPrimitive(), currentTorque.AsPrimitive(), GearRole.Consumer)
        {
            GeneratedElectricPower = suppliedPower.AsPrimitive();
            GenerationState = generationState;
        }
```
（Key 7・9 は欠番のままにする。MessagePack のキーは詰め直さない）

- [ ] **Step 5: Tracker の単体テストを書く**

```csharp
using Game.Block.Blocks.GearToElectric;
using Game.Gear.Common;
using NUnit.Framework;
using UniRx;

namespace Tests.UnitTest.Game
{
    public class GearToElectricGenerationTrackerTest
    {
        // 最低RPM未満は渡した電力があっても停止中。最低RPMちょうどなら発電中
        // Below minimum RPM is stopped even with handed-over power; exactly the minimum is generating
        [Test]
        public void BelowMinimumRpmIsStoppedEvenWithSuppliedPower()
        {
            var tracker = new GearToElectricGenerationTracker(40f);
            tracker.RecordElectricTick(new RPM(39f), 50f);
            Assert.AreEqual(GearToElectricGenerationState.Stopped, tracker.State);

            tracker.RecordElectricTick(new RPM(40f), 50f);
            Assert.AreEqual(GearToElectricGenerationState.Generating, tracker.State);
        }

        // 最低RPMが0でもRPM0は停止中
        // RPM 0 is stopped even when the minimum RPM is 0
        [Test]
        public void ZeroRpmIsStoppedWhenMinimumRpmIsZero()
        {
            var tracker = new GearToElectricGenerationTracker(0f);
            tracker.RecordElectricTick(new RPM(0f), 50f);
            Assert.AreEqual(GearToElectricGenerationState.Stopped, tracker.State);
        }

        // 回っていて渡した電力が0なら待機中
        // Spinning with zero handed-over power is idle
        [Test]
        public void SpinningWithoutSuppliedPowerIsIdle()
        {
            var tracker = new GearToElectricGenerationTracker(40f);
            tracker.RecordElectricTick(new RPM(40f), 0f);
            Assert.AreEqual(GearToElectricGenerationState.Idle, tracker.State);
        }

        // 初回は必ず通知し、同じ値の記録では通知せず、供給電力か状態が変われば通知する
        // The first record always notifies; an identical record does not; a change in supplied power or state does
        [Test]
        public void NotifiesOnlyWhenSuppliedPowerOrStateChanges()
        {
            var tracker = new GearToElectricGenerationTracker(40f);
            var notifyCount = 0;
            tracker.OnGenerationChanged.Subscribe(_ => notifyCount++);

            tracker.RecordElectricTick(new RPM(40f), 50f);
            Assert.AreEqual(1, notifyCount);
            Assert.AreEqual(50f, tracker.SuppliedPower, 0.001f);

            tracker.RecordElectricTick(new RPM(40f), 50f);
            Assert.AreEqual(1, notifyCount);

            tracker.RecordElectricTick(new RPM(40f), 30f);
            Assert.AreEqual(2, notifyCount);

            tracker.RecordElectricTick(new RPM(10f), 30f);
            Assert.AreEqual(3, notifyCount);
            Assert.AreEqual(GearToElectricGenerationState.Stopped, tracker.State);
        }
    }
}
```
`RPM` は `Game.Gear.Common.RPM`（`new RPM(float)` で作れる）。

- [ ] **Step 6: コンポーネントの結合テストを書く**

テスト用modの `TestGearToElectricGenerator` は `minimumRpm: 0`・`baseRpm: 120` なので、停止中はRPM0で確かめる（minimumRpm の分岐は Step 5 が固定する）。電線は張らない。孤立したセグメントとして需要0の電力tickが毎tick届くことも、このテストで固定する。
```csharp
using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.GearToElectric;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Core.Gear
{
    public class GearToElectricGenerationStateTest
    {
        // 電線なしで回すと待機中。需要のある放電で発電中。次の実tick（需要0）で待機中・供給電力0に戻り、状態変化イベントが発火する。RPM0は停止中
        // Spinning unwired is idle; a discharge with demand is generating; the next real tick (no demand) returns to idle with zero supply and fires a state-change event; RPM 0 is stopped
        [Test]
        public void UnwiredGeneratorReportsIdleGeneratingAndStopped()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestGearToElectricGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out var driveBlock);

            var generatorComponent = generatorBlock.GetComponent<GearToElectricGeneratorComponent>();
            var driveComponent = driveBlock.GetComponent<SimpleGearGeneratorComponent>();
            var param = (GearToElectricGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearToElectricGenerator).BlockParam;

            // 状態変化イベントはインターフェース経由で購読する（BlockSystem と同じ経路）
            // Subscribe to state changes through the interface (the same path BlockSystem uses)
            var stateChangeCount = 0;
            generatorBlock.GetComponent<IBlockStateObservable>().OnChangeBlockState.Subscribe(_ => stateChangeCount++);

            // 回っていて需要が無ければ待機中・供給電力0
            // Spinning with no demand is idle with zero supply
            driveComponent.SetGenerateRpm(param.GearConsumption.BaseRpm);
            driveComponent.SetGenerateTorque(param.GearConsumption.BaseTorque);
            AdvanceTime(0.5f);
            Assert.AreEqual(GearToElectricGenerationState.Idle, ReadDetail().GenerationState);
            Assert.AreEqual(0f, ReadDetail().GeneratedElectricPower, 0.001f);

            // 需要40%の統計で放電させると発電中になり、渡した電力が載り、イベントが発火する
            // Discharging with 40% demand statistics becomes generating, carries the handed-over power, and fires an event
            var beforeDischargeCount = stateChangeCount;
            var supply = generatorComponent.OutputEnergy().AsPrimitive();
            generatorComponent.OnElectricTickPostProcess(new ElectricNetworkStatistics(supply, supply * 0.4f, 1f, 1));
            Assert.AreEqual(GearToElectricGenerationState.Generating, ReadDetail().GenerationState);
            Assert.AreEqual(supply * 0.4f, ReadDetail().GeneratedElectricPower, 0.01f);
            Assert.Greater(stateChangeCount, beforeDischargeCount);

            // 次の実tickは需要0の電力tickなので、前回の放電量を残さず待機中へ戻り、イベントが発火する
            // The next real tick is a zero-demand electric tick, so it drops the previous discharge, returns to idle, and fires an event
            var beforeTickCount = stateChangeCount;
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(GearToElectricGenerationState.Idle, ReadDetail().GenerationState);
            Assert.AreEqual(0f, ReadDetail().GeneratedElectricPower, 0.001f);
            Assert.Greater(stateChangeCount, beforeTickCount);

            // RPM0は停止中
            // RPM 0 is stopped
            driveComponent.SetGenerateRpm(0f);
            AdvanceTime(0.5f);
            Assert.AreEqual(GearToElectricGenerationState.Stopped, ReadDetail().GenerationState);

            #region Internal

            GearToElectricGeneratorBlockStateDetail ReadDetail()
            {
                var detail = generatorComponent.GetBlockStateDetails().First(d => d.Key == GearToElectricGeneratorBlockStateDetail.GearGeneratorBlockStateDetailKey);
                return MessagePackSerializer.Deserialize<GearToElectricGeneratorBlockStateDetail>(detail.Value);
            }

            #endregion
        }

        private static void AdvanceTime(double seconds)
        {
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < seconds)
            {
                GameUpdater.UpdateOneTick();
            }
        }
    }
}
```
`BlockStateDetail` のプロパティ名（`Key`/`Value`）は `FuelGearGeneratorTest.cs:342-343` の使い方と同じ。`AdvanceTime` は既存の `GearToElectricGeneratorTest.cs:102-109` と同じ書き方（実時間ループで tick を回す既存テストの流儀。ゲームロジックではないので時間規約の対象外）。

- [ ] **Step 7: コンパイルしてテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GearToElectric|ElectricTickUnification"`
Expected: ErrorCount 0。既存の `GearToElectricGeneratorTest`・`ElectricTickUnificationTest` を含めて全件PASS

- [ ] **Step 8: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/GearToElectric moorestech_server/Assets/Scripts/Tests
git commit -m "feat(server): 回転発電機の発電状態と供給電力を状態詳細で送る"
```

### Task 2: クライアント — 状態詳細をWebUI用のDTOへ詰める

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/GearToElectricDetailDtoBuilder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtos.cs`（末尾に DTO を追加）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockInventoryDtos.cs`（`ElectricToGear` の次に1フィールド）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs`（`ElectricToGearDetailDtoBuilder.Apply` の次に1行）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BlockDetail/GearToElectricDetailDtoBuilderTest.cs`

**Interfaces:**
- Consumes: `GearToElectricGeneratorBlockStateDetail.GenerationState` / `.GeneratedElectricPower`（Task 1）
- Produces:
  - `public class GearToElectricDetailDto { public string GenerationState; public float SuppliedPower; public float MaxGeneratedPower; }`
  - `BlockInventoryDto.GearToElectric`（`public GearToElectricDetailDto GearToElectric;`）
  - JSONキー: `gearToElectric: { generationState: "generating"|"idle"|"stopped", suppliedPower: number, maxGeneratedPower: number }`（既存のシリアライザがキーを camelCase に変える。`electricToGear` と同じ）

- [ ] **Step 1: DTO を足す**

`BlockDetailDtos.cs` の `ElectricToGearOutputModeDto` の後ろに追加する:
```csharp
    public class GearToElectricDetailDto
    {
        // generating / idle / stopped
        public string GenerationState;
        public float SuppliedPower;
        public float MaxGeneratedPower;
    }
```
`BlockInventoryDtos.cs` の `public ElectricToGearDetailDto ElectricToGear;` の次に `public GearToElectricDetailDto GearToElectric;` を追加する。

- [ ] **Step 2: Builder を作る**

```csharp
using Client.Game.InGame.Block;
using Game.Block.Blocks.GearToElectric;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 回転発電機の発電状態・供給電力・最大発電量をDTOへ詰める
    /// Fills the rotary generator's generation state, supplied power and max generated power into its DTO
    /// </summary>
    public static class GearToElectricDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, object param)
        {
            if (param is not GearToElectricGeneratorBlockParam generatorParam) return;
            var state = block.GetStateDetail<GearToElectricGeneratorBlockStateDetail>(
                GearToElectricGeneratorBlockStateDetail.GearGeneratorBlockStateDetailKey);
            if (state == null) return;

            dto.GearToElectric = BuildDetail(state, generatorParam);
        }

        // 状態はサーバーの判定をそのまま写し、最大はマスタから引く
        // Copy the server-decided state as is and take the maximum from master
        public static GearToElectricDetailDto BuildDetail(GearToElectricGeneratorBlockStateDetail state, GearToElectricGeneratorBlockParam param)
        {
            return new GearToElectricDetailDto
            {
                GenerationState = BlockDetailDtoBuilder.ToCamelCase(state.GenerationState.ToString()),
                SuppliedPower = state.GeneratedElectricPower,
                MaxGeneratedPower = (float)param.MaxGeneratedPower,
            };
        }
    }
}
```
`BlockDetailDtoBuilder.Apply` の `ElectricToGearDetailDtoBuilder.Apply(dto, block, param);` の次の行に `GearToElectricDetailDtoBuilder.Apply(dto, block, param);` を追加する。

- [ ] **Step 3: テストを書く**

```csharp
using Client.WebUiHost.Game.Topics.BlockDetail;
using Core.Master;
using Game.Block.Blocks.GearToElectric;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.WebUi.BlockDetail
{
    /// <summary>
    /// 回転発電機のDTOがサーバーの状態をcamelCaseで写し、最大値をマスタから引くことを固定する
    /// Pins that the rotary generator DTO copies the server state in camelCase and takes the maximum from master
    /// </summary>
    public class GearToElectricDetailDtoBuilderTest
    {
        [TestCase(GearToElectricGenerationState.Generating, "generating")]
        [TestCase(GearToElectricGenerationState.Idle, "idle")]
        [TestCase(GearToElectricGenerationState.Stopped, "stopped")]
        public void BuildDetailCopiesStateAndMasterMaximum(GearToElectricGenerationState state, string expectedState)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var param = (GearToElectricGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearToElectricGenerator).BlockParam;
            var detail = new GearToElectricGeneratorBlockStateDetail(true, new RPM(120f), new Torque(100f), new ElectricPower(72f), state);

            var dto = GearToElectricDetailDtoBuilder.BuildDetail(detail, param);

            Assert.AreEqual(expectedState, dto.GenerationState);
            Assert.AreEqual(72f, dto.SuppliedPower, 0.001f);
            Assert.AreEqual(100f, dto.MaxGeneratedPower, 0.001f);
        }
    }
}
```
`RPM`/`Torque`/`ElectricPower` の生成の形は Task 1 と同じく実物に合わせる。

- [ ] **Step 4: コンパイルしてテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.WebUi.BlockDetail.GearToElectricDetailDtoBuilderTest"`
Expected: ErrorCount 0 / 3件PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail moorestech_client/Assets/Scripts/Client.Tests/WebUi/BlockDetail
git commit -m "feat(client): 回転発電機の状態詳細をblock_inventoryのDTOへ詰める"
```

### Task 3: WebUI — 契約・文言・セクション

**Files:**
- Create: `moorestech_web/webui/src/bridge/contract/schemas/energyConversion.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/inventory.ts`（ElectricToGear 系 schema を移して import に置き換え、`gearToElectric` を追加。今199行なので移さないと200行を超える）
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/index.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/payloadTypes.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/validators.test.ts`（受理・拒否のケースを1つずつ）
- Modify: `Localization/localization.csv`（4キー）
- Modify（生成）: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`
- Modify: `moorestech_web/webui/src/features/blockInventory/details/detailLogic.ts`
- Create: `moorestech_web/webui/src/features/blockInventory/details/GearToElectricSection.tsx`
- Create: `moorestech_web/webui/src/features/blockInventory/details/GearToElectricSection.test.ts`
- Modify: `moorestech_web/webui/src/features/blockInventory/views/SectionStackView.tsx`

**Interfaces:**
- Consumes: JSONキー `gearToElectric`（Task 2）
- Produces: `GearToElectricDataSchema`, `GearToElectricGenerationStateSchema`, 型 `GearToElectricData`, `GearToElectricGenerationState`, `gearToElectricStateDisplay(state)`, コンポーネント `GearToElectricSection`

- [ ] **Step 1: 契約を分けて追加する**

`energyConversion.ts`（`inventory.ts:124-134` の ElectricToGear 系2つをそのまま移し、新しい schema を足す）:
```ts
import { z } from "zod";

// 電力と歯車の変換機（電気→歯車／歯車→電気）の詳細契約
// Detail contracts for the electricity/gear converters (electric→gear and gear→electric)
export const ElectricToGearOutputModeDataSchema = z.object({
  rpm: z.number(),
  torque: z.number(),
  requiredPower: z.number(),
});
export const ElectricToGearDataSchema = z.object({
  selectedIndex: z.number().int().nonnegative(),
  fulfillmentRate: z.number(),
  consumedElectricPower: z.number(),
  outputModes: z.array(ElectricToGearOutputModeDataSchema),
});

export const GearToElectricGenerationStateSchema = z.enum(["generating", "idle", "stopped"]);
export const GearToElectricDataSchema = z.object({
  generationState: GearToElectricGenerationStateSchema,
  suppliedPower: z.number(),
  maxGeneratedPower: z.number(),
}).strict();
```
`inventory.ts`: 移した2つの定義を削除し、先頭に `import { ElectricToGearDataSchema, GearToElectricDataSchema } from "./energyConversion";` を足す。`BlockInventoryOpenSchema` の `electricToGear: ...optional(),` の次に `gearToElectric: GearToElectricDataSchema.optional(),` を足す。
`index.ts`: `export * from "./energyConversion";` を足す。
`payloadTypes.ts`: import 列に `GearToElectricDataSchema`, `GearToElectricGenerationStateSchema` を足し、`ElectricToGearData` の型定義の次に追加する:
```ts
export type GearToElectricGenerationState = z.infer<typeof GearToElectricGenerationStateSchema>;
export type GearToElectricData = z.infer<typeof GearToElectricDataSchema>;
```
`payloadTypes.ts` が型を `@/bridge` から再公開している経路（`ElectricToGearData` と同じ）に載ることを確かめる。

- [ ] **Step 2: 契約のテストを足す**

`validators.test.ts` の `it("rejects electricToGear without output mode power", ...)` の後ろに足す（`openBase`・`parseTopicPayload`・`Topics` は同ファイルの既存のものを使う）:
```ts
  it("accepts a gearToElectric detail", () => {
    expect(parseTopicPayload(Topics.blockInventory, {
      ...openBase,
      blockType: "GearToElectricGenerator",
      gearToElectric: { generationState: "idle", suppliedPower: 0, maxGeneratedPower: 100 },
    }).valid).toBe(true);
  });

  it("rejects a gearToElectric detail with an unknown generation state", () => {
    expect(parseTopicPayload(Topics.blockInventory, {
      ...openBase,
      blockType: "GearToElectricGenerator",
      gearToElectric: { generationState: "charging", suppliedPower: 0, maxGeneratedPower: 100 },
    }).valid).toBe(false);
  });
```

- [ ] **Step 3: 文言を足してキーを生成する**

`Localization/localization.csv` の `ui.blockInventory.machineStateIdle` 行の近くに追加する:
```csv
ui.blockInventory.gearToElectricStateGenerating,Generating,Generating,発電中,Erzeugt Strom,발전 중
ui.blockInventory.gearToElectricStateIdle,Standby (no demand),Standby (no demand),待機中（需要なし）,Bereit (kein Bedarf),대기 중 (수요 없음)
ui.blockInventory.gearToElectricStateStopped,Stopped (RPM too low),Stopped (RPM too low),停止中（RPM不足）,Gestoppt (Drehzahl zu niedrig),정지 (RPM 부족)
ui.blockInventory.gearToElectricSuppliedPower,Supplied power {current} / max {max},Supplied power {current} / max {max},供給電力 {current} / 最大 {max},Gelieferte Leistung {current} / max. {max},공급 전력 {current} / 최대 {max}
```
Run: `pnpm --dir moorestech_web/webui gen:i18n`
Expected: `generated/localizationKeys.ts` に4キーが増える。差分はこの4キーだけ。

- [ ] **Step 4: 表示の表を足す**

`detailLogic.ts` の `machineStateDisplay` の表の後ろに追加する（`GearToElectricGenerationState` を `@/bridge` から import する）:
```ts
// 発電状態のラベルと不足トーンを1つの表で決める。停止中だけ赤（機械の halted と同じ扱い）
// One table settles the generation-state label and insufficient tone; only stopped is red (same as a machine's halted)
export type GearToElectricStateDisplay = {
  labelKey: TranslationKey;
  insufficient: boolean;
};

export function gearToElectricStateDisplay(state: GearToElectricGenerationState): GearToElectricStateDisplay {
  return GearToElectricStateDisplayTable[state];
}

const GearToElectricStateDisplayTable: Record<GearToElectricGenerationState, GearToElectricStateDisplay> = {
  generating: { labelKey: L.ui.blockInventory.gearToElectricStateGenerating, insufficient: false },
  idle: { labelKey: L.ui.blockInventory.gearToElectricStateIdle, insufficient: false },
  stopped: { labelKey: L.ui.blockInventory.gearToElectricStateStopped, insufficient: true },
};
```

- [ ] **Step 5: セクションを作る**

```tsx
import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import LackHighlightText from "./LackHighlightText";
import { gearToElectricStateDisplay } from "./detailLogic";
import { L, useI18n } from "@/shared/i18n";

// 回転発電機: 発電状態と供給電力（この1台が網へ渡した量 / マスタ最大）。網の「発電」とは別の値
// Rotary generator: generation state and supplied power (handed to the network by this unit / master max); distinct from the network's "generated"
export default function GearToElectricSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  if (!data.gearToElectric) return null;
  const detail = data.gearToElectric;
  const display = gearToElectricStateDisplay(detail.generationState);
  return (
    <Stack gap={2} data-testid="gear-to-electric-section">
      <LackHighlightText insufficient={display.insufficient} size="sm" testId="gear-to-electric-state">{t(display.labelKey)}</LackHighlightText>
      <Text size="sm" c="var(--text-default)" data-testid="gear-to-electric-supplied-power">
        {t(L.ui.blockInventory.gearToElectricSuppliedPower, {
          current: detail.suppliedPower.toFixed(0),
          max: detail.maxGeneratedPower.toFixed(0),
        })}
      </Text>
    </Stack>
  );
}
```

- [ ] **Step 6: 汎用ビューへ差し込む**

`SectionStackView.tsx`:
- import に `import GearToElectricSection from "../details/GearToElectricSection";` を足す。
- `configByBlockType` に、空のグリッド・液体行を出さない設定を足す:
```ts
  GearToElectricGenerator: { itemGridTestId: null, fluidRowTestId: null, renderEmptyGrid: false, showFluidProgress: false },
```
- `<GearSection data={data} />` の直前に `<GearToElectricSection data={data} />` を置く。これで描画順は「状態・供給電力 → 歯車行 → 電力網 → 歯車網」になる（Requirements 2）。

- [ ] **Step 7: セクションのテストを書く**

`GearSection.test.ts` と同じモックの形（`useI18n` を `key|params` を返す形に、Mantine を素の要素に）で書く。`LackHighlightText` は実物を使い、`data-insufficient` 属性を見る（`GearSection.test.ts:38` が同じ属性を見ている。属性名は `LackHighlightText/index.tsx` の実物に合わせる）。
```ts
import GearToElectricSection from "./GearToElectricSection";

function render(gearToElectric: GearToElectricData) {
  const data = { open: true, source: "block", blockType: "GearToElectricGenerator", identifier: "block:1", blockGuid: "g", itemSlots: [], fluidSlots: [], gearToElectric } as unknown as BlockInventoryOpen;
  let tree!: ReturnType<typeof create>;
  act(() => { tree = create(createElement(GearToElectricSection, { data })); });
  return tree.root;
}

describe("GearToElectricSection", () => {
  it("renders generating with supplied power over master max, without the insufficient tone", () => {
    const root = render({ generationState: "generating", suppliedPower: 72.4, maxGeneratedPower: 100 });
    expect(textOf(root, "gear-to-electric-state")).toBe("ui.blockInventory.gearToElectricStateGenerating|undefined");
    expect(textOf(root, "gear-to-electric-supplied-power")).toBe('ui.blockInventory.gearToElectricSuppliedPower|{"current":"72","max":"100"}');
    expect(root.findAll((n) => n.props["data-insufficient"] === true)).toHaveLength(0);
  });
  it("renders idle without the insufficient tone", () => {
    const root = render({ generationState: "idle", suppliedPower: 0, maxGeneratedPower: 100 });
    expect(textOf(root, "gear-to-electric-state")).toBe("ui.blockInventory.gearToElectricStateIdle|undefined");
    expect(root.findAll((n) => n.props["data-insufficient"] === true)).toHaveLength(0);
  });
  it("renders stopped with the insufficient tone", () => {
    const root = render({ generationState: "stopped", suppliedPower: 0, maxGeneratedPower: 100 });
    expect(textOf(root, "gear-to-electric-state")).toBe("ui.blockInventory.gearToElectricStateStopped|undefined");
    expect(root.findAll((n) => n.props["data-insufficient"] === true).length).toBeGreaterThan(0);
  });
  it("renders nothing without a gearToElectric detail", () => {
    const data = { open: true, source: "block", blockType: "Shaft", identifier: "block:1", blockGuid: "g", itemSlots: [], fluidSlots: [] } as unknown as BlockInventoryOpen;
    let tree!: ReturnType<typeof create>;
    act(() => { tree = create(createElement(GearToElectricSection, { data })); });
    expect(tree.toJSON()).toBeNull();
  });
});
```
`textOf` は `GearSection.test.ts:25-28` と同じ関数を置く。`LackHighlightText` が子要素を入れ子にする場合は、`textOf` を testId の要素の下のテキストを連結して読む形に直す（期待文字列は変えない）。

- [ ] **Step 8: WebUIの検査を通す**

Run:
- `pnpm --dir moorestech_web/webui test`
- `pnpm --dir moorestech_web/webui lint`
- `pnpm --dir moorestech_web/webui exec tsc -b`

Expected: すべて成功（vitest 全件PASS、lint 0 error、型エラー 0）。`tsc -b` が出力物を作った場合はコミットしない。

- [ ] **Step 9: コミットする**

```bash
git add moorestech_web/webui/src Localization/localization.csv
git commit -m "feat(webui): 回転発電機の発電状態と供給電力の行を汎用ブロックパネルに足す"
```

### Task 4: マスタ — 回転発電機を開けるようにする

**Files:**
- Modify（moorestech_master）: `server_v8/mods/moorestechAlphaMod_8/master/blocks.json`（`"name": "回転発電機"` のブロックの `"openable": false` → `true`）
- Modify: `.moorestech-external-revisions.json`（`moorestech_master.commitHash`）

**Interfaces:**
- Produces: v8マスタで回転発電機の `Openable == true`

- [ ] **Step 1: masterのworktreeを、本repoがピンしているコミットから作る**

```bash
git -C /Users/sakastudio/hermes-agent/data/repos/moorestech_master fetch origin
git -C /Users/sakastudio/hermes-agent/data/repos/moorestech_master worktree add -b feature/rotary-generator-openable /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/rotary-generator-openable 47f79caa83c55b1c945dc3d86b1a91c5a0e5c3b3
```
（土台は本repoの現在のピン `47f79caa`。origin/master はこの後にスキーマ側の変更を含むことがあるので、ピンの上に積む）

- [ ] **Step 2: openable を true にする**

`blocks.json` の `"blockGuid": "019e158c-df55-7001-be7c-eca97046ca41"` のブロックで、`"openable": false,` を `"openable": true,` にする。ほかの行は変えない。
Run: `git -C <master worktree> diff --stat`
Expected: `blocks.json` の1行だけ

- [ ] **Step 3: masterをコミット・pushしてPRを作る**

```bash
git -C <master worktree> commit -am "回転発電機を開けるようにする (openable: true)"
git -C <master worktree> push -u origin feature/rotary-generator-openable
gh pr create --repo moorestech/moorestech_master --head feature/rotary-generator-openable --base master --title "回転発電機を開けるようにする" --body "moorestech本体の回転発電機UI（feature/rotary-generator-ui）のため、回転発電機のopenableをtrueにする。"
```

- [ ] **Step 4: 本repoのピンを更新する**

`.moorestech-external-revisions.json` の `moorestech_master` の `commitHash` を、Step 3 でpushしたコミット（`git -C <master worktree> rev-parse HEAD`）にする。ローカルのUnityがこのコミットのマスタを読むよう、moores-wt の運用（`docs/development/worktree-and-review-pitfalls.md` の「master ピン」節）に従ってピンworktreeを合わせる。

- [ ] **Step 5: コミットする**

```bash
git add .moorestech-external-revisions.json
git commit -m "chore: 回転発電機をopenableにしたmasterへピンを更新"
```

### Task 5: unityプレイ録画テスト

**Files:**
- Create: スキル `unity-playmode-recorded-playtest` の規約が定めるシナリオの置き場に `RotaryGeneratorUiScenario`（パスはスキルの `references/write-scenario.md` に従う）

- [ ] **Step 1: シナリオを書く**

`unity-playmode-recorded-playtest` スキルの `references/write-scenario.md` のテンプレートに従う。流れ:
1. `SetupDebugEnvironment` → `SkipOpeningSkit`
2. `PlaceBlockDirect("回転発電機", 原点, North)` → `WaitBlockGameObject`
3. 駆動源なしで発電機を照準（`AimAt`）→ `PressInteract()` → `UntilWebUiElement("gear-to-electric-section")` → `Screenshot("rotary-generator-stopped")`。`gear-to-electric-state` が「停止中（RPM不足）」であることを、画面を読んで確かめる
4. パネルを閉じる → 発電機の歯車コネクタ（v8マスタ: offset `[2,1,0]`、向き `[0,0,-1]`）に、回せる駆動源（v8マスタには SimpleGearGenerator が無く、歯車の駆動源は FuelGearGenerator の燃料式風車〈コネクタ offset [0,0,2]〉と蒸気機関〈offset [2,1,7]〉の2種。燃料式風車を使い、`ServerService` 経由で燃料を入れる。回転発電機は baseRpm=minimumRpm=40 なので、RPMが40を割ると停止中になる点に注意）を `PlaceBlockDirect` でつなぐ。電力の消費機械を `PlaceBlockDirect` で置き、電線でつなぐ（`Hotbar.UnlockConnectTool` → 接続ツール。手順は `references/hotbar-driven-systems.md`）
5. 再び開いて `Screenshot("rotary-generator-generating")`。「発電中」、「供給電力 N / 最大 100」（N>0）、歯車行、電力網・歯車網の各セクションが出ていることを確かめる
6. 消費機械を `RemoveBlock` で外す → `Screenshot("rotary-generator-idle")`。「待機中（需要なし）」で、供給電力が0になることを確かめる

駆動源と配線の組み立てが複雑なら、スキルの「複雑シナリオはStep 0（サブエージェント探索）を先に」に従い、先に探索する。

- [ ] **Step 2: 実行して結果を確かめる**

Run: スキル同梱の `scripts/run-scenario.sh`（引数はスキルの `references/run-scenario.md` どおり）
Expected: result.json が成功。3枚のスクリーンショットで、状態行・供給電力行・歯車行・網セクションの順に並び、停止中だけが赤であることを目で確かめる。録画とスクリーンショットのパスを報告に書く

- [ ] **Step 3: コミットする**

```bash
git add <シナリオファイル>
git commit -m "test(playtest): 回転発電機UIの3状態を録画で確かめるシナリオ"
```

### Task 6: 全ブランチレビュー

- [ ] 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー指摘の反映が判定の経路・条件式・その評価時点（`ResolveState`・`RecordElectricTick`・`DischargeByUtilization`）に触れたら、Task 5 の unityプレイ録画テストを反映後のバイナリで再実施してから完了とする

### Task 7: PR作成とセッション終了可能状態

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。moorestech_master 側のPR（Task 4）のURLもPR本文に書く。全実装タスク完了＝完了ではない。PR未作成のまま終わるのはplan未完了である

---

## 判断記録（ADR）

短い選択ラベルは名称・汎用ビュー・状態行追加の根拠とし、位置・数量・閾値・網維持などの詳細承認とは区別する。詳細の承認根拠はユーザーの実行指示「以下の実装planを実行してください」「まずplan全文を読み、`## Requirements`・`## Global Constraints`・`## 判断記録（ADR）`を全タスク共通の制約として扱ってください」とRequirements。元の質問文は未発見であり、再構成しない。

ADRは作らない（どの決定も、戻しにくさ・事情を知らないと意外・本当のトレードオフの3条件をそろえないため）。裁定の正本は `.decisions/` の次の4件。

| 決定 | 出所 |
|---|---|
| 回転発電機を開けるようにし、汎用ビューに電力行を1本足す。歯車行・歯車網・電力網は既存部品。専用ビュー案と、openableだけ案は棄却 | ユーザー裁定 2026-10-13 原文「回転式発電機のUIを作りたい」→ 選択「汎用ビュー＋発電行」（[[2026-10-13-回転発電機UIは汎用ビューに発電量行を足す]]） |
| 状態行「発電中／待機中（需要なし）／停止中（RPM不足）」を足す。RPM不足の判定は既存の minimumRpm（現在40）。RPM行は赤にしない | ユーザー裁定 2026-10-13 選択「状態行を足す」（[[2026-10-13-回転発電機UIに発電状態行を足す]]） |
| 電力行のラベルは「供給電力 現在 / 最大 N」。電力網欄の「発電」の文言は変えない | ユーザー裁定 2026-10-13 選択「「供給電力」にする」（[[2026-10-13-回転発電機UIの電力行は供給電力と呼ぶ]]） |
| 状況確認専用（手動の開始・停止なし）。内部バッテリー残量は出さない | ユーザー裁定 2026-10-13 選択「状況を見るだけ」「出さない」（[[2026-10-13-回転発電機UIは表示専用で内部蓄電は出さない]]） |
| 発電状態はサーバーのコンポーネントが判定し、状態詳細で送る（クライアントでRPMとマスタから導出しない） | agent前提（機械の稼働状態 `CommonMachineBlockStateDetail.CurrentStateType` はサーバーが判定して送っている前例。判断は具体側で行う原則） |
| RPMが0以下でも停止中とする（minimumRpm が 0 のマスタでも、RPM0は発電しない） | agent前提（`GearToElectricGeneratorComponent.ChargeFromGear` の「RPM0は稼働率0で充電しない」） |
| 停止中だけ不足トーン（赤）にし、待機中は赤にしない | agent前提（機械の `halted` だけを insufficient にする `MachineStateDisplayTable` の前例。裁定時のプレビュー図の「←赤」注記は裁定事項に含めない） |
| 状態と供給電力は電力tickの後処理（放電の直後）で確定・通知し、`Update()` には手を入れない | シミュレーター予測（判事review）を agent が裏取りして適用 2026-10-13（電線0本でも孤立セグメントとして電力tickが毎tick届く: `VanillaGearToElectricGeneratorTemplate.cs:35`・`ElectricWireTopologyMap.cs:37-48`・`MasterTickUpdater.cs:51-68`。AGENTS.md「Update()内で毎tickの同値判定をしない」） |
| 発電の可視値の変化は、基底の歯車通知と `Merge` して状態変化イベントで送る | agent前提（`ElectricToGearGeneratorComponent` の、基底の通知を自前の通知へ合流させる前例。サーバー可変状態の同期の3点セット） |
| ElectricToGear 系の zod schema を `energyConversion.ts` へ移す | agent前提（`inventory.ts` が199行で、追加すると200行を超えるための分割。変換機2種を同じ責務でまとめる） |
| 状態詳細の `ChargeRate`/`BatteryRemaining` を削除する | シミュレーター予測（判事review・Warning）を適用 2026-10-13（クライアント・テストとも利用0件。グローバル規約「既知の負債を据え置く案を推奨しない」） |
| unityプレイ録画テストを含める（Task 5） | agent前提（UIとブロックの開閉に触れる変更なので、単体テストに加えて通しで確かめる） |
| 発電状態は、サーバー enum / DTO の camelCase 文字列 / zod enum の形のまま（DTOをenumにしない） | シミュレーター予測（確信高）による前提宣言 2026-10-13（`MachineDetailDto.CurrentState`・`GearNetworkDto.StopReason` と同じ形。違えばユーザーが拒否権で覆す） |
| 新しい行の t() に欠損時の特別扱いを足さない | シミュレーター予測（確信高）による前提宣言 2026-10-13（既存行と同じ扱い。キーは生成型で存在保証） |
| Phase 2.6 弱指摘は (b)(d) だけ採る: 状態と供給電力は電力tickで1回だけ確定し、状態詳細はそれを読むだけ。(a) 括弧書きの表示側導出は据え置く。(c) は非表示値（ChargeRate/BatteryRemaining）を削除したので解消 | シミュレーター予測（確信高）による前提宣言 2026-10-13 |

## 本PR外のリファクタ提案（Phase 2.6 第3バケツ）

- 既存側 `moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/GearConsumptionCalculator.cs:26`（`CalcOperatingRate` の `rpm < MinimumRpm` 判定）／新設側 `GearToElectricGenerationTracker.ResolveState`／同じ役割と判断した根拠: どちらも「minimumRpm 未満なら動かない」という同じ判定を持つ。着手するかは人間が決める
