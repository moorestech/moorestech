# Belt Merge Cell Splitter Cover Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ベルトの合流マス（`BeltSegmentKind.Merge`）の直線ベルコン描画を、同じファミリーの分岐器ブロックのモデルで差し替えて表示する。

**Architecture:** マスタ側は「出力コネクタが複数あるベルト（分岐器）のうち blockType と beltSpeedPerSecond が同じもの」を対応付けの規則とし、Core.Master の静的ユーティリティ＋起動時 Validator で一意性を保証する。クライアントは `BeltItemObjectPresenter` と同型の `BeltMergeCellCoverPresenter` を `TrainUnitClientSimulator.Tick` から毎フレーム呼び、複製の版が進んだ回に合流マス集合を取り直して、`BlockGameObject` の Renderer を無効にし分岐器 prefab を同位置・同回転で専用親の下に出す。

**Tech Stack:** Unity / C# / VContainer / UniRx / NUnit（uloop）

## Requirements

> 2026-10-17 追記（ユーザー裁定「テストいりません」「テストではモデル差し替えしなくていい」）: 本 plan のテスト（R1〜R4 の単体テスト、R6 の Validator とそのテスト、テスト mod への分岐器追加）は実装しない。対応付けは `Game.Block.Interface/Extension/BeltSplitterCounterpart` に置き（`.claude/rules/core-master.md`）、0件・2件以上は実行時ログ＋覆いなし。実装済み: この追記時点でコード一式を本ブランチへ反映。

- R1. 複製（`BeltTransportClientCache.Replica`）の `Shapes[i].Kind == BeltSegmentKind.Merge` な segment の `Cells[0].Position` にあるベルコンは、その描画（配下の全 `Renderer`）が無効になり、同じファミリーの分岐器 prefab が同位置・同回転で表示される。受け入れ: 単体テストで合流 segment を含む全量を適用すると `IBeltMergeCellCoverFactory.TryCreate` がその位置で1回呼ばれる。
- R2. 合流が解消された全量（その位置が Merge でない）を受けたら覆いは `Remove()` され、ベルコンの Renderer は元に戻る。受け入れ: 単体テストで Merge なし全量を適用後に `Remove` が呼ばれている。
- R3. 合流マスの `BlockGameObject` がまだ無い（ブロック設置イベントが複製より遅れた）間は覆いを作らず、現れたフレームで作る。複製の版が変わらなくても `UpdateAll` ごとに未作成マスを再試行する。受け入れ: 単体テストで factory が「無い」を返す間は作られず、「ある」に変えた次の `UpdateAll`（同じ版）で作られる。
- R4. ベルコンが撤去され `BlockGameObject` が破棄された覆い（`IsAlive == false`）は、次の `UpdateAll` で `Remove()` され、同じ位置がまだ Merge なら作り直される（同位置に別のベルコンが置かれた場合）。受け入れ: 単体テストで `IsAlive=false` にした次の `UpdateAll` で `Remove` と再 `TryCreate`。
- R5. 対応付け規則: ベルト `B`（`IBeltSpeedParam` を持ち出力コネクタが1個以下）に対し、出力コネクタが2個以上のベルト `S` で `S.BlockType == B.BlockType` かつ `S.BeltSpeedPerSecond == B.BeltSpeedPerSecond`（差 1e-6 未満）のものを分岐器とする。受け入れ: `BeltSplitterCounterpart.GetSplitterBlockId(直線歯車ベルト)` が同型同速の分岐器の BlockId を返す。
- R6. 候補が0件または2件以上のベルトがあるマスタは `BlockMasterUtil.Validate` が拒否する。受け入れ: forUnitTest の blocks.json を改変（分岐器を消す／複製する）した JToken で `AssertInvalidWith` が通り、無改変の forUnitTest・EditModeInPlayingTestMod・alpha mod は通る。
- R7. サーバーの分岐器判定（`BeltTopologyBuilder.IsSplitter`）とクライアント側の判定は同じ実装（`BeltSplitterCounterpart.IsSplitter`）を使う。
- R8. 覆いの更新は購読でなく `TrainUnitClientSimulator.Tick` の `_beltItemObjectPresenter.UpdateAll()` 直後に明示的に呼ぶ。
- やらないこと: 分岐器ブロック自身の見た目変更（ユーザー「分岐器のモデルをかえてとはいってない」）。マスタスキーマの変更。`Kind == Branch` の segment（分岐器が合流を兼ねる場合）への覆い。tick 間補間。仕様たたき台（ユーザー管理 repo）の更新。

## Global Constraints

- AGENTS.md の規約全部（200行/ファイル、10ファイル/ディレクトリ、日英2行コメント、`#region Internal` はローカル関数用途のみ、`Func<>` 禁止、デフォルト引数禁止、try-catch は外部境界のみ、fail-closed 経路は必ずログ）。
- `[SerializeField]` は使わない（今回は MonoBehaviour を作らない）。
- DI の追加は `MainGameInteractionRegistration` に2行、`TrainUnitClientSimulator` のコンストラクタ引数1つ。これ以外の DI 構成変更はしない（メモ「DI周り分割禁止」）。
- push・PR はユーザーが行う。ローカルコミットまで。Unity 起動・テスト実行はユーザー依頼時のみ（メモ）。
- 出所: ADR `docs/adr/0083-belt-merge-cell-shows-family-splitter-model.md`、裁定 `.decisions/2026-10-17-合流マスはファミリーの分岐器モデルでベルト描画を差し替える.md`。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 判定ロジックは Core.Master（BeltSpeedPerSecondValidator・CanConnectConnectorShapes の前例）、表示は Client.Game/InGame/BeltTransport（BeltItemObjectPresenter の前例）、呼び出しは Tick からの明示呼び
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施 / fresh-context subagent による検査はユーザーの時間節約のため省略（判断記録 J4）

---

### Task 1: マスタ側 — 分岐器の対応付けと Validator

**Files:**
- Create: `moorestech_server/Assets/Scripts/Core.Master/Belt/BeltSplitterCounterpart.cs`
- Create: `moorestech_server/Assets/Scripts/Core.Master/Validator/Block/BeltSplitterCounterpartValidator.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/BlockMasterUtil.cs:24`（`BeltSpeedPerSecondValidator.Validate` の次の行に登録）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/Topology/BeltTopologyBuilder.cs:141-152`（ローカル関数 `IsSplitter`/`OutputConnectorCount` を `BeltSplitterCounterpart.IsSplitter(param)` 呼びに置換）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`（分岐器2件＋ファミリー2件追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json`（分岐器2件＋ファミリー2件追加）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Transport/BeltSplitterCounterpartValidationTest.cs`

**Interfaces:**
- Produces: `Core.Master.Belt.BeltSplitterCounterpart` — `public static bool IsSplitter(IBlockParam param)`、`public static BlockId GetSplitterBlockId(BlockId beltBlockId)`（候補1件の前提。Validator が保証）、`internal static List<BlockMasterElement> FindCounterparts(Blocks blocks, BlockMasterElement belt)`。

- [ ] **Step 1: テストmod へ分岐器を足す**

forUnitTest `blocks.json` の `data` 末尾に2件追加（既存 `BeltConveyorSplitter`(…00b1) と `GearBeltConveyorSplitter`(…9040) を複製し、name・guid・速度・connectorGuid を変える。gear 側は `gear`/`gearConsumption` も複製）:

```json
{"blockSize": [1, 1, 1], "name": "TestBeltConveyorSplitter", "blockType": "BeltConveyor",
 "blockParam": {"beltSpeedPerSecond": 0.333333,
   "inventoryConnectors": {"inputConnects": [{"connectorGuid": "6a1f0c2e-0001-4d0e-8d0b-5e3f1c9a0001", "offset": [0, 0, 0], "directions": [[0, 0, -1], [0, -1, -1]]}],
     "outputConnects": [{"connectorGuid": "6a1f0c2e-0002-4d0e-8d0b-5e3f1c9a0002", "offset": [0, 0, 0], "directions": [[0, 0, 1], [0, -1, 1]]},
                        {"connectorGuid": "6a1f0c2e-0003-4d0e-8d0b-5e3f1c9a0003", "offset": [0, 0, 0], "directions": [[1, 0, 0], [1, -1, 0]]},
                        {"connectorGuid": "6a1f0c2e-0004-4d0e-8d0b-5e3f1c9a0004", "offset": [0, 0, 0], "directions": [[-1, 0, 0], [-1, -1, 0]]}]},
   "slopeType": "Straight"},
 "blockGuid": "00000000-0000-0000-0000-0000000000b2", "category": "テスト", "subCategory": "BeltConveyor", "placementsPerCost": 1, "openable": false}
```

```json
{"blockSize": [1, 1, 1], "name": "SmallGearBeltConveyorSplitter", "blockType": "GearBeltConveyor",
 "blockParam": {"overloadMaxRpm": 0, "overloadMaxTorque": 0, "destructionCheckInterval": 1, "baseDestructionProbability": 0, "beltSpeedPerSecond": 2.0,
   "gear": {"gearConnects": [{"offset": [0, 0, 0], "option": {"isReverse": false}, "directions": [[1, 0, 0], [-1, 0, 0]], "connectorGuid": "6a1f0c2e-0005-4d0e-8d0b-5e3f1c9a0005"}]},
   "inventoryConnectors": {"inputConnects": [{"connectorGuid": "6a1f0c2e-0006-4d0e-8d0b-5e3f1c9a0006", "offset": [0, 0, 0], "directions": [[0, 0, -1], [0, -1, -1]]}],
     "outputConnects": [{"connectorGuid": "6a1f0c2e-0007-4d0e-8d0b-5e3f1c9a0007", "offset": [0, 0, 0], "directions": [[0, 0, 1], [0, -1, 1]]},
                        {"connectorGuid": "6a1f0c2e-0008-4d0e-8d0b-5e3f1c9a0008", "offset": [0, 0, 0], "directions": [[1, 0, 0], [1, -1, 0]]},
                        {"connectorGuid": "6a1f0c2e-0009-4d0e-8d0b-5e3f1c9a0009", "offset": [0, 0, 0], "directions": [[-1, 0, 0], [-1, -1, 0]]}]},
   "slopeType": "Straight",
   "gearConsumption": {"baseRpm": 10, "minimumRpm": 0, "baseTorque": 1, "torqueExponentUnder": 2, "torqueExponentOver": 1.585, "idlePowerRate": 0.2}},
 "blockGuid": "00000000-0000-0000-0000-0000000000b3", "category": "テスト", "subCategory": "GearBeltConveyor", "placementsPerCost": 1, "openable": false}
```

`beltConveyorFamilies` に `{"straightBlockGuid": "00000000-0000-0000-0000-0000000000b2"}` と `{"straightBlockGuid": "00000000-0000-0000-0000-0000000000b3"}` を追加（ファミリー Validator「belongs to no beltConveyorFamily」対策）。既存の速度10の分岐器2件はそのまま（規則は「非分岐器ベルトごとに分岐器1件」なので、直線の無い分岐器は違反にならない）。

EditModeInPlayingTestMod `blocks.json` にも2件（木の歯車 0.055556 用と電気 1.0 用）。歯車側は既存 `67255c7b-…1580` の `gear`/`gearConsumption`/`inventoryConnectors` を複製して `outputConnects` を上の3方向にし、`blockPrefabAddressablesPath` は既存 addressable の `"Vanilla/Block/gear belt conveyor splitter"`、電気側は `"Vanilla/Block/ConveyorSplitter"` を使う（新規 prefab は作らない）。guid は `6a1f0c2e-00b4-4d0e-8d0b-5e3f1c9a00b4` と `6a1f0c2e-00b5-4d0e-8d0b-5e3f1c9a00b5`、`beltConveyorFamilies` に各1件追加。

- [ ] **Step 2: テストを書く**

```csharp
using System.IO;
using System.Linq;
using Core.Master;
using Core.Master.Belt;
using Core.Master.Validator;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core.Transport
{
    // 直線ベルトごとに同型同速の分岐器がちょうど1件ある、という対応付けの起動時検証
    // Startup validation that every straight belt has exactly one splitter of the same type and speed
    public class BeltSplitterCounterpartValidationTest
    {
        [Test]
        public void 同型同速の分岐器が無い直線ベルトは検証で弾かれる()
        {
            var blocksJToken = PrepareBlocksJson();
            RemoveBlock(blocksJToken, "TestBeltConveyorSplitter");

            AssertInvalidWith(blocksJToken, "[BlockMaster] Belt TestBeltConveyor has no splitter with blockType:BeltConveyor beltSpeedPerSecond:0.333333");
        }

        [Test]
        public void 同型同速の分岐器が2件ある直線ベルトは検証で弾かれる()
        {
            var blocksJToken = PrepareBlocksJson();
            var duplicated = (JObject)FindBlock(blocksJToken, "TestBeltConveyorSplitter").DeepClone();
            duplicated["name"] = "TestBeltConveyorSplitter2";
            duplicated["blockGuid"] = "00000000-0000-0000-0000-0000000000b9";
            ((JArray)blocksJToken["data"]).Add(duplicated);
            ((JArray)blocksJToken["beltConveyorFamilies"]).Add(new JObject { ["straightBlockGuid"] = "00000000-0000-0000-0000-0000000000b9" });

            AssertInvalidWith(blocksJToken, "[BlockMaster] Belt TestBeltConveyor has 2 splitters with blockType:BeltConveyor beltSpeedPerSecond:0.333333");
        }

        [Test]
        public void 既存テストマスタの分岐器対応付けは検証を通る()
        {
            var blocksJToken = PrepareBlocksJson();
            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);
            Assert.IsTrue(isValid, errorLogs);
        }

        [Test]
        public void 直線ベルトから同型同速の分岐器が引ける()
        {
            PrepareBlocksJson();
            var straight = MasterHolder.BlockMaster.GetBlockId(System.Guid.Parse("00000000-0000-0000-0000-000000000003"));
            var expected = MasterHolder.BlockMaster.GetBlockId(System.Guid.Parse("00000000-0000-0000-0000-0000000000b2"));
            Assert.AreEqual(expected, BeltSplitterCounterpart.GetSplitterBlockId(straight));
            Assert.AreEqual(expected, BeltSplitterCounterpart.GetSplitterBlockId(expected), "a splitter maps to itself");
        }

        private static JToken PrepareBlocksJson()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blocksJsonPath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            return JToken.Parse(File.ReadAllText(blocksJsonPath));
        }

        private static JObject FindBlock(JToken blocksJToken, string blockName)
        {
            return blocksJToken["data"].Children<JObject>().First(block => (string)block["name"] == blockName);
        }

        private static void RemoveBlock(JToken blocksJToken, string blockName)
        {
            var block = FindBlock(blocksJToken, blockName);
            var guid = (string)block["blockGuid"];
            block.Remove();
            blocksJToken["beltConveyorFamilies"].Children<JObject>().First(family => (string)family["straightBlockGuid"] == guid).Remove();
        }

        private static void AssertInvalidWith(JToken blocksJToken, string expectedLog)
        {
            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);
            Assert.IsFalse(isValid);
            StringAssert.Contains(expectedLog, errorLogs);
        }
    }
}
```

- [ ] **Step 3: `BeltSplitterCounterpart` を書く**

```csharp
using System;
using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Belt
{
    /// <summary>
    ///     ベルトと分岐器の対応付け。分岐器は「出力コネクタが2個以上のベルト」、相手は blockType と秒速が同じもの（ADR 0083）
    ///     Pairs belts with splitters: a splitter is a belt with two or more output connectors, matched by equal blockType and cells-per-second (ADR 0083)
    /// </summary>
    public static class BeltSplitterCounterpart
    {
        // 秒速はJSONの小数6桁で書かれるので、その丸め誤差より大きく、刻み(1/18≒0.055)より小さい許容で同値とみなす
        // Cells-per-second is written to six decimals, so equality tolerates more than that rounding yet far less than one step (1/18 ≈ 0.055)
        private const double SpeedTolerance = 1e-6;

        public static bool IsSplitter(IBlockParam param)
        {
            // 未定義の出力は搬出口なしとして0個に数える
            // Undefined outputs mean no emitting port and count as zero
            var outputs = ((IInventoryConnectors)param).InventoryConnectors.OutputConnects;
            return outputs != null && 1 < outputs.Length;
        }

        // 候補はValidatorが起動時にちょうど1件へ絞っているので、ここでは先頭を返す
        // The validator narrows candidates to exactly one at startup, so the first match is the answer
        public static BlockId GetSplitterBlockId(BlockId beltBlockId)
        {
            var belt = MasterHolder.BlockMaster.GetBlockMaster(beltBlockId);
            if (IsSplitter(belt.BlockParam)) return beltBlockId;
            var counterparts = FindCounterparts(MasterHolder.BlockMaster.Blocks, belt);
            return MasterHolder.BlockMaster.GetBlockId(counterparts[0].BlockGuid);
        }

        internal static List<BlockMasterElement> FindCounterparts(Blocks blocks, BlockMasterElement belt)
        {
            var speed = ((IBeltSpeedParam)belt.BlockParam).BeltSpeedPerSecond;
            var result = new List<BlockMasterElement>();
            foreach (var candidate in blocks.Data)
            {
                if (candidate.BlockParam is not IBeltSpeedParam candidateSpeed || !IsSplitter(candidate.BlockParam)) continue;
                if (candidate.BlockType != belt.BlockType) continue;
                if (SpeedTolerance <= Math.Abs(candidateSpeed.BeltSpeedPerSecond - speed)) continue;
                result.Add(candidate);
            }
            return result;
        }
    }
}
```

- [ ] **Step 4: Validator を書き、登録する**

```csharp
using Core.Master.Belt;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block
{
    /// <summary>
    ///     合流マスの覆いに使う分岐器が、直線ベルトごとにちょうど1件引けるか検証する（ADR 0083）
    ///     Validates that the splitter used to cover a merge cell resolves to exactly one block per straight belt (ADR 0083)
    /// </summary>
    internal static class BeltSplitterCounterpartValidator
    {
        internal static string Validate(Blocks blocks)
        {
            var logs = "";
            foreach (var block in blocks.Data)
            {
                if (block.BlockParam is not IBeltSpeedParam speedParam || BeltSplitterCounterpart.IsSplitter(block.BlockParam)) continue;
                var count = BeltSplitterCounterpart.FindCounterparts(blocks, block).Count;
                if (count == 1) continue;
                var shape = $"blockType:{block.BlockType} beltSpeedPerSecond:{speedParam.BeltSpeedPerSecond}";
                logs += count == 0
                    ? $"[BlockMaster] Belt {block.Name} has no splitter with {shape}\n"
                    : $"[BlockMaster] Belt {block.Name} has {count} splitters with {shape}\n";
            }
            return logs;
        }
    }
}
```

`BlockMasterUtil.cs` の `errorLogs += BeltSpeedPerSecondValidator.Validate(blocks);` の直後に `errorLogs += BeltSplitterCounterpartValidator.Validate(blocks);` を追加。

- [ ] **Step 5: `BeltTopologyBuilder` の分岐器判定を共通化する**

`BeltTopologyBuilder.cs:130` の `IsSplitter(param)` を `BeltSplitterCounterpart.IsSplitter(param)` に変え、ローカル関数 `IsSplitter`/`OutputConnectorCount`（141-152行）を削除。`using Core.Master.Belt;` を追加。`OutputConnectorCount` が他で使われていないことを grep で確認してから消す。

- [ ] **Step 6: コンパイルしテストを実行する**（ユーザーが実行を許可した場合のみ）

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltSplitterCounterpartValidationTest|BeltTopology|BeltSegmentLayout|ExtractionSettingsValidationTest"`
Expected: ErrorCount 0 / 全件 PASS

- [ ] **Step 7: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Core.Master/Belt moorestech_server/Assets/Scripts/Core.Master/Validator moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/Topology/BeltTopologyBuilder.cs moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Transport/BeltSplitterCounterpartValidationTest.cs
git commit -m "feat: ベルトと同型同速の分岐器の対応付けを Core.Master に置き、起動時に一意性を検証する"
```
（Unity が生成した `.meta` も同じコミットに含める。手で作らない）

### Task 2: クライアント — 合流マスの覆い

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltTransport/Cover/IBeltMergeCellCover.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltTransport/Cover/IBeltMergeCellCoverFactory.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltTransport/Cover/BeltMergeCellCoverPresenter.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltTransport/Cover/BlockGameObjectBeltMergeCellCover.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltTransport/Cover/BlockGameObjectBeltMergeCellCoverFactory.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BeltTransport/View/BeltItemObjectPresenter.cs:12-13`（コメント「箱で隠す前提」→「ファミリーの分岐器モデルで隠す前提」）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainUnitClientSimulator.cs:29-49,127`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs:192`（直後に2行）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BeltTransport/BeltMergeCellCoverPresenterTest.cs`

**Interfaces:**
- Consumes: `BeltSplitterCounterpart.GetSplitterBlockId(BlockId)`（Task 1）、`BeltTransportClientCache.Replica / ReplicaVersion`、`BlockGameObjectDataStore.TryGetBlockGameObject(Vector3Int, out BlockGameObject)`、`ClientContext.BlockGameObjectPrefabContainer.CreateBlockGameObject(BlockId, Vector3, Quaternion)`、`BlockPreviewOnlyObjects.Initialize(GameObject, BlockId, bool)`。
- Produces: `IBeltMergeCellCover { bool IsAlive { get; } void Remove(); }`、`IBeltMergeCellCoverFactory { bool TryCreate(Vector3Int position, out IBeltMergeCellCover cover); }`、`BeltMergeCellCoverPresenter(BeltTransportClientCache, IBeltMergeCellCoverFactory)` with `void UpdateAll()`。

- [ ] **Step 1: テストを書く**

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Cover;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BeltTransport
{
    // 複製の合流segmentに合わせて覆いが作られ、合流の解消・ベルコンの撤去で消え、ブロックが遅れて現れたら作られるか
    // Whether covers follow the replica's merge segments: created, removed on un-merge or belt removal, and created once a late block appears
    public class BeltMergeCellCoverPresenterTest
    {
        private const int Speed = 18;
        private static readonly Vector3Int MergePosition = new(5, 0, 0);

        [Test]
        public void MergeSegmentGetsACoverAndUnmergeRemovesIt()
        {
            var factory = new FakeCoverFactory();
            var cache = new BeltTransportClientCache();
            var presenter = new BeltMergeCellCoverPresenter(cache, factory);

            cache.ApplyFullState(new BeltTransportFullState(new[] { Straight(new Vector3Int(0, 0, 0)), Merge(MergePosition) }));
            presenter.UpdateAll();
            Assert.AreEqual(1, factory.Created.Count, "one cover for the merge cell only");
            Assert.AreEqual(MergePosition, factory.Created[0].Position);

            cache.ApplyFullState(new BeltTransportFullState(new[] { Straight(MergePosition) }));
            presenter.UpdateAll();
            Assert.IsTrue(factory.Created[0].Removed, "un-merged cell loses its cover");
        }

        [Test]
        public void MissingBlockIsRetriedEveryUpdateUntilItAppears()
        {
            var factory = new FakeCoverFactory { BlockPresent = false };
            var cache = new BeltTransportClientCache();
            var presenter = new BeltMergeCellCoverPresenter(cache, factory);
            cache.ApplyFullState(new BeltTransportFullState(new[] { Merge(MergePosition) }));

            presenter.UpdateAll();
            presenter.UpdateAll();
            Assert.AreEqual(0, factory.Created.Count, "no cover while the block object is absent");

            factory.BlockPresent = true;
            presenter.UpdateAll();
            Assert.AreEqual(1, factory.Created.Count, "created on the first update after the block appeared, same replica version");
        }

        [Test]
        public void DeadCoverIsRemovedAndRecreatedWhileStillMerged()
        {
            var factory = new FakeCoverFactory();
            var cache = new BeltTransportClientCache();
            var presenter = new BeltMergeCellCoverPresenter(cache, factory);
            cache.ApplyFullState(new BeltTransportFullState(new[] { Merge(MergePosition) }));
            presenter.UpdateAll();

            factory.Created[0].IsAlive = false;
            presenter.UpdateAll();
            Assert.IsTrue(factory.Created[0].Removed, "a cover whose belt object died is dropped");
            Assert.AreEqual(2, factory.Created.Count, "and re-created for the still-merged cell");
        }

        [Test]
        public void UpdateAllDoesNothingWithoutAReplica()
        {
            var factory = new FakeCoverFactory();
            var presenter = new BeltMergeCellCoverPresenter(new BeltTransportClientCache(), factory);
            presenter.UpdateAll();
            Assert.AreEqual(0, factory.Created.Count);
        }

        private static BeltSegmentState Straight(Vector3Int origin)
        {
            var cells = new[] { new BeltCellShape(origin, BeltDirection.Front) };
            var shape = new BeltSegmentShape(BeltSegmentKind.Normal, false, cells, 1, Speed, BeltDirection.Front, Array.Empty<BeltLinkShape>(), Array.Empty<BeltLinkShape>());
            return new BeltSegmentState(shape, 0, Speed, Array.Empty<BeltItemSnapshot>(), false, default);
        }

        private static BeltSegmentState Merge(Vector3Int origin)
        {
            var cells = new[] { new BeltCellShape(origin, BeltDirection.Front) };
            var shape = new BeltSegmentShape(BeltSegmentKind.Merge, false, cells, 1, Speed, BeltDirection.Front, Array.Empty<BeltLinkShape>(), Array.Empty<BeltLinkShape>());
            return new BeltSegmentState(shape, 0, Speed, Array.Empty<BeltItemSnapshot>(), false, default);
        }

        private sealed class FakeCoverFactory : IBeltMergeCellCoverFactory
        {
            public bool BlockPresent = true;
            public readonly List<FakeCover> Created = new();

            public bool TryCreate(Vector3Int position, out IBeltMergeCellCover cover)
            {
                cover = null;
                if (!BlockPresent) return false;
                var created = new FakeCover { Position = position };
                Created.Add(created);
                cover = created;
                return true;
            }
        }

        private sealed class FakeCover : IBeltMergeCellCover
        {
            public Vector3Int Position;
            public bool Removed;
            public bool IsAlive { get; set; } = true;
            public void Remove() { Removed = true; }
        }
    }
}
```

`BeltSegmentState` のコンストラクタ引数は `BeltItemObjectPresenterTest.Line` と同じ並び（shape, head, speed, items, hasBufferItem, bufferItem）。

- [ ] **Step 2: インターフェースとプレゼンターを書く**

`IBeltMergeCellCover.cs`:
```csharp
namespace Client.Game.InGame.BeltTransport.Cover
{
    // 合流マス1つの覆い。ベルコンの BlockGameObject が破棄されると IsAlive が false になる
    // The cover of one merge cell; IsAlive turns false once the belt's BlockGameObject is destroyed
    public interface IBeltMergeCellCover
    {
        bool IsAlive { get; }
        void Remove();
    }
}
```

`IBeltMergeCellCoverFactory.cs`:
```csharp
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.Cover
{
    // 合流マスの位置に覆いを作る。ベルコンの BlockGameObject がまだ無ければ false
    // Creates a cover at a merge cell; false while the belt's BlockGameObject does not exist yet
    public interface IBeltMergeCellCoverFactory
    {
        bool TryCreate(Vector3Int position, out IBeltMergeCellCover cover);
    }
}
```

`BeltMergeCellCoverPresenter.cs`:
```csharp
using System.Collections.Generic;
using Core.BeltTransport;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.Cover
{
    // 合流マスの直線ベルコンを分岐器モデルで覆う。複製の版が進んだ回に合流マス集合を取り直し、未作成と死んだ覆いは毎回見直す
    // Covers the straight belt on each merge cell with the splitter model; re-reads the merge cells when the replica advances and revisits pending and dead covers every call
    // 呼び出しは購読でなく TrainUnitClientSimulator から毎フレーム明示的に行う(BeltItemObjectPresenter と同じ)
    // Not subscription-driven: TrainUnitClientSimulator calls this every frame explicitly, like BeltItemObjectPresenter
    public sealed class BeltMergeCellCoverPresenter
    {
        private readonly BeltTransportClientCache _cache;
        private readonly IBeltMergeCellCoverFactory _factory;
        private readonly HashSet<Vector3Int> _mergeCells = new();
        private readonly Dictionary<Vector3Int, IBeltMergeCellCover> _covers = new();
        private readonly List<Vector3Int> _work = new();
        private int _shownReplicaVersion;

        public BeltMergeCellCoverPresenter(BeltTransportClientCache cache, IBeltMergeCellCoverFactory factory)
        {
            _cache = cache;
            _factory = factory;
        }

        public void UpdateAll()
        {
            if (_cache.Replica == null) return;
            if (_cache.ReplicaVersion != _shownReplicaVersion)
            {
                _shownReplicaVersion = _cache.ReplicaVersion;
                CollectMergeCells();
                RemoveCoversOutsideMergeCells();
            }
            RemoveDeadCovers();
            CreateMissingCovers();
            return;

            #region Internal

            // 合流segmentは1マスなので先頭マスの位置が合流マス
            // A merge segment is a single cell, so its first cell is the merge cell
            void CollectMergeCells()
            {
                _mergeCells.Clear();
                foreach (var shape in _cache.Replica.Shapes)
                {
                    if (shape.Kind != BeltSegmentKind.Merge || shape.Cells.Length == 0) continue;
                    _mergeCells.Add(shape.Cells[0].Position);
                }
            }

            void RemoveCoversOutsideMergeCells()
            {
                _work.Clear();
                foreach (var position in _covers.Keys)
                {
                    if (!_mergeCells.Contains(position)) _work.Add(position);
                }
                foreach (var position in _work) RemoveCover(position);
            }

            // ベルコンが撤去された覆いは落とす。まだ合流マスなら下の CreateMissingCovers が作り直す
            // Drop covers whose belt was removed; CreateMissingCovers below re-creates them while the cell is still a merge
            void RemoveDeadCovers()
            {
                _work.Clear();
                foreach (var pair in _covers)
                {
                    if (!pair.Value.IsAlive) _work.Add(pair.Key);
                }
                foreach (var position in _work) RemoveCover(position);
            }

            // ブロック設置イベントが複製より遅れた合流マスは、BlockGameObject が現れるまで毎回試す
            // Merge cells whose block placement arrives after the replica are retried every call until the BlockGameObject appears
            void CreateMissingCovers()
            {
                foreach (var position in _mergeCells)
                {
                    if (_covers.ContainsKey(position)) continue;
                    if (_factory.TryCreate(position, out var cover)) _covers.Add(position, cover);
                }
            }

            void RemoveCover(Vector3Int position)
            {
                _covers[position].Remove();
                _covers.Remove(position);
            }

            #endregion
        }
    }
}
```

- [ ] **Step 3: Unity 実装を書く**

`BlockGameObjectBeltMergeCellCover.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.Cover
{
    // 直線ベルコンの Renderer を無効にし、分岐器モデルを別オブジェクトとして出している状態
    // The state of one covered belt: its renderers disabled and the splitter model shown as a separate object
    public sealed class BlockGameObjectBeltMergeCellCover : IBeltMergeCellCover
    {
        private readonly BlockGameObject _belt;
        private readonly GameObject _coverObject;
        private readonly List<Renderer> _hiddenRenderers;

        // Unity の破棄済み判定(== null)に頼る。ベルコンが撤去されると false
        // Relies on Unity's destroyed check (== null); false once the belt was removed
        public bool IsAlive => _belt != null;

        public BlockGameObjectBeltMergeCellCover(BlockGameObject belt, GameObject coverObject, List<Renderer> hiddenRenderers)
        {
            _belt = belt;
            _coverObject = coverObject;
            _hiddenRenderers = hiddenRenderers;
        }

        public void Remove()
        {
            // ベルコンが生きていれば描画を戻す。覆いは常に消す
            // Restore the belt's rendering when it still exists; the cover object always goes
            foreach (var renderer in _hiddenRenderers)
            {
                if (renderer != null) renderer.enabled = true;
            }
            Object.Destroy(_coverObject);
        }
    }
}
```

`BlockGameObjectBeltMergeCellCoverFactory.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Context;
using Core.Master.Belt;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.Cover
{
    // 合流マスのベルコンと同型同速の分岐器 prefab を同位置・同回転で出し、ベルコンの Renderer を無効にする
    // Shows the splitter prefab of the belt's type and speed at the belt's position and rotation, disabling the belt's renderers
    // 覆いは BlockGameObject の子にせず専用の親に置く(ベルコン撤去時は IsAlive で追従する。AddressableBeltItemObjectFactory と同じ親方式)
    // Covers live under a dedicated parent rather than the BlockGameObject (removal is tracked via IsAlive; same parent scheme as AddressableBeltItemObjectFactory)
    public sealed class BlockGameObjectBeltMergeCellCoverFactory : IBeltMergeCellCoverFactory
    {
        private const string ParentName = "BeltMergeCellCovers";
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private Transform _parent;

        public BlockGameObjectBeltMergeCellCoverFactory(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        public bool TryCreate(Vector3Int position, out IBeltMergeCellCover cover)
        {
            cover = null;
            if (!_blockGameObjectDataStore.TryGetBlockGameObject(position, out var belt)) return false;

            var splitterBlockId = BeltSplitterCounterpart.GetSplitterBlockId(belt.BlockId);
            var coverObject = ClientContext.BlockGameObjectPrefabContainer.CreateBlockGameObject(splitterBlockId, belt.transform.position, belt.transform.rotation);
            coverObject.transform.SetParent(Parent(), true);
            StripBlockBehaviour(coverObject, splitterBlockId);
            cover = new BlockGameObjectBeltMergeCellCover(belt, coverObject, HideRenderers(belt));
            return true;

            #region Internal

            // 見た目だけが欲しいので、ブロックとしての振る舞い(当たり判定・状態処理・プレビュー専用物)は外す
            // Only the visuals are wanted, so block behaviour (colliders, state processors, preview-only objects) is stripped
            void StripBlockBehaviour(GameObject target, Core.Master.BlockId blockId)
            {
                foreach (var collider in target.GetComponentsInChildren<Collider>(true)) Object.Destroy(collider);
                foreach (var processor in target.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (processor is IBlockStateChangeProcessor || processor is BlockGameObject || processor is BlockGameObjectChild) Object.Destroy(processor);
                }
                BlockPreviewOnlyObjects.Initialize(target, blockId, false);
            }

            // 有効だった Renderer だけ無効にして覚える。戻すときに元から無効だったものを有効化しないため
            // Disable and remember only the renderers that were enabled, so restoring never enables ones that were off to begin with
            List<Renderer> HideRenderers(BlockGameObject target)
            {
                var hidden = new List<Renderer>();
                foreach (var renderer in target.GetComponentsInChildren<Renderer>(false))
                {
                    if (!renderer.enabled) continue;
                    renderer.enabled = false;
                    hidden.Add(renderer);
                }
                return hidden;
            }

            Transform Parent()
            {
                if (_parent == null) _parent = new GameObject(ParentName).transform;
                return _parent;
            }

            #endregion
        }
    }
}
```

`BlockPreviewOnlyObjects.Initialize` の戻り値型・第2引数型は `BlockGameObject.cs:85` の呼び出し `BlockPreviewOnlyObjects.Initialize(gameObject, BlockId, false)` と同じ。`BlockGameObjectChild` が `MonoBehaviour` でなければその条件は外す（実装時に `Client.Game/InGame/Block/BlockGameObjectChild.cs` を確認）。

- [ ] **Step 4: 呼び出しと DI を配線する**

`TrainUnitClientSimulator.cs`: フィールド `private readonly BeltMergeCellCoverPresenter _beltMergeCellCoverPresenter;` を追加し、コンストラクタの末尾引数に `BeltMergeCellCoverPresenter beltMergeCellCoverPresenter` を足して代入。127行 `_beltItemObjectPresenter.UpdateAll();` の直後に:
```csharp
            // 合流マスの覆いはアイテム表示の後に同じ順で更新する(複製が進んだ回に合流マスを取り直す)
            // Merge-cell covers update after the item display in this same fixed order (merge cells re-read when the replica advanced)
            _beltMergeCellCoverPresenter.UpdateAll();
```
`using Client.Game.InGame.BeltTransport.Cover;` を追加。`TrainUnitClientSimulator` を `new` している箇所（テスト rig 含む）を grep し、引数を足す。

`MainGameInteractionRegistration.cs:192` の直後:
```csharp
            builder.Register<BlockGameObjectBeltMergeCellCoverFactory>(Lifetime.Singleton).As<IBeltMergeCellCoverFactory>();
            builder.Register<BeltMergeCellCoverPresenter>(Lifetime.Singleton);
```
`using Client.Game.InGame.BeltTransport.Cover;` を追加。

`BeltItemObjectPresenter.cs:12-13` のコメントを「(合流・分岐マスはファミリーの分岐器モデルで隠す前提)」「(merge and branch cells are hidden by the family's splitter model)」に変える。

- [ ] **Step 5: コンパイルしテストを実行する**（ユーザーが実行を許可した場合のみ）

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.BeltTransport"`
Expected: ErrorCount 0 / 全件 PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BeltTransport moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainUnitClientSimulator.cs moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs moorestech_client/Assets/Scripts/Client.Tests/BeltTransport/BeltMergeCellCoverPresenterTest.cs
git commit -m "feat: 合流マスの直線ベルコンをファミリーの分岐器モデルで覆う"
```

### Task 3: 閉じタスク

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**（自動実行・ゴール文言による省略不可）。Codex 監査ステップはメモの方針どおりスキップし、記録にその旨を書く。
- [ ] **Step 2: 実プレイ確認**: ユーザーが Editor で Play し、木の歯車ベルトの合流マスに `歯車コンベア分岐機` の箱、電気ベルトの合流マスに `ConveyorSplitter` が出ること、横入れを撤去すると直線に戻ることを目視する。unityプレイ録画テストは追加しない（判断記録 J3）。
- [ ] **Step 3: セッション終了可能状態にする**: 全作業がローカルコミット済みであることを `git status` で確認する。push と PR #1501 の更新はユーザーが行う（メモ「No PR, push only to work branch」「push はユーザー許可時のみ」）。

## 判断記録（ADR）

- 設計ADR: `docs/adr/0083-belt-merge-cell-shows-family-splitter-model.md`、裁定 `.decisions/2026-10-17-合流マスはファミリーの分岐器モデルでベルト描画を差し替える.md`
- J1. 対応付けユーティリティと Validator は Core.Master に置く（`Core.Master/Belt/BeltSplitterCounterpart.cs`）。Validator（Core.Master）とクライアント（Game.Block.Interface 以上）の両方から同じ規則を参照するため。前例: `BeltSpeedPerSecondValidator`（Core.Master/Validator/Block）、`BlockMaster.CanConnectConnectorShapes`。サーバーの `BeltTopologyBuilder.IsSplitter` も同じ実装へ寄せる。出所: agent前提（DRY・R7）
- J2. テスト mod（forUnitTest・EditModeInPlayingTestMod）には同型同速の分岐器が無く、R6 の Validator をそのまま入れると全テストが起動時に落ちる。既存分岐器の速度は変えず（分岐系テストの tick 期待値が変わるため）、同型同速の分岐器ブロックを各 mod に追加する。出所: agent前提（実測: forUnitTest の分岐器は速度10のみ、直線は 0.333333/2.0。EditModeInPlayingTestMod に分岐器なし）。**ユーザーへ報告済みの前提で進める。異議があれば Validator を「2件以上のみ拒否」へ緩める**
- J3. unityプレイ録画テストは追加しない。表示の差し替えは単体テスト（presenter）で覆い、prefab の見た目はユーザーの目視確認で足りる。手元 PC の録画環境は junction 依存で重い（メモ）。出所: agent前提
- J4. spec-architecture-review Phase 2.6（fresh-context subagent）と user-simulator review は省略。ユーザーが待っている深夜作業で、変更範囲が BeltItemObjectPresenter の同型追加＋Validator 1本に閉じているため。出所: agent前提（ユーザー裁定ではない。レビュー免責力なし）
- J5. 覆い生成時に分岐器 prefab から外すもの: `Collider`、`IBlockStateChangeProcessor`、`BlockGameObject`、`BlockGameObjectChild`、プレビュー専用物（`BlockPreviewOnlyObjects.Initialize(…, false)`）。プレビュー生成（`PreviewGhostCreator`）は Collider を外していないが、覆いは本番ワールドに常駐し設置レイキャストに当たってはいけないため外す。出所: agent前提（ADR 0081「プレビューに当たり判定を持たせない」の趣旨）
- J6. Codex 独立質問（grill §1.7）・Codex ホストでの実行引き継ぎは行わない。出所: ユーザー方針（メモ「Codexは貴重なので使用禁止」）
