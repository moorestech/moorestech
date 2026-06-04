# ElectricToGearGenerator Implementation Plan (Plan 1: サーバーサイド)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 電力を消費してギア動力を生成する新ブロック `ElectricToGearGenerator` をサーバーサイドで実装する。出力は `{rpm, torque, requiredPower}` の離散テーブルから選び、選択した RPM は固定、電力不足時はトルクがドループする。選択モードはランタイムにプロトコルで切替え・永続化する。

**Architecture:** 既存 `GearToElectricGenerator` の逆。`GearEnergyTransformer` を継承し `IGearGenerator`（ギア生成）と `IElectricConsumer`（電力消費）を実装する単一コンポーネント。`RequestEnergy` は選択エントリの `requiredPower` で固定なのでフィードバックループは無い。選択状態 `selectedIndex` は `IBlockSaveState` で save/load し、`SetElectricToGearOutputModeProtocol` で切り替える。

**Tech Stack:** C# / Unity / moorestech サーバー。Mooresmaster SourceGenerator（YAML→C#）、MessagePack、NUnit。`uloop` CLI（compile / run-tests）。

**スコープ外（別 Plan）:** クライアント側のモード選択 UI（Unity prefab/UI、別ツーリング）と、実プレイ用 Mod データ・アイテム画像（`../moorestech_master`）。本 Plan はサーバーコア＋プロトコル＋自動テストのみで、NUnit で完結して動作確認できる。

**前提となる設計図解:** `/Users/katsumi/infographics/electric-to-gear-generator`（`http://127.0.0.1:5180/`）。

---

## 実装するファイル一覧

| 役割 | パス | 新規/変更 |
|---|---|---|
| スキーマ定義 | `VanillaSchema/blocks.yml` | 変更（enum + when ブロック追加） |
| 自動生成 | `Mooresmaster.Model.BlocksModule.*`（`BlockTypeConst` / `ElectricToGearGeneratorBlockParam`） | 自動生成（手書き禁止） |
| 状態同期クラス | `moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricToGear/ElectricToGearGeneratorBlockStateDetail.cs` | 新規 |
| 本体コンポーネント | `moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricToGear/ElectricToGearGeneratorComponent.cs` | 新規 |
| テンプレート | `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaElectricToGearGeneratorTemplate.cs` | 新規 |
| テンプレート登録 | `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs` | 変更（1 行） |
| プロトコル | `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/SetElectricToGearOutputModeProtocol.cs` | 新規 |
| プロトコル登録 | `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs` | 変更（1 行） |
| テスト用 Mod ブロック | `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json` | 変更（エントリ追加） |
| テスト用 Mod アイテム | `.../forUnitTest/master/items.json` | 変更（エントリ追加） |
| テスト用 BlockId | `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs` | 変更（1 行） |
| コンポーネント挙動テスト | `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/ElectricToGearGeneratorTest.cs` | 新規 |
| プロトコルテスト | `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricToGearOutputModeProtocolTest.cs` | 新規 |
| クライアント送電範囲表示 | `moorestech_client/Assets/Scripts/Client.Game/InGame/Electric/DisplayEnergizedRange.cs` | 変更（1 行） |

**重要な gotcha（着手前に必読）:**
- `.cs` を編集したら必ず `uloop compile --project-path ./moorestech_client` を実行する（AGENTS.md）。
- **新規のサーバー側 `.cs`（特に新規テストクラス）は、Unity の Refresh では認識されず、Unity の RESTART が必要**（memory: `server-tests-immutable-package`）。新規テストを `run-tests` で走らせる前に Unity を再起動する。
- YAML スキーマ編集は **`edit-schema` スキルを必ず参照**（AGENTS.md）。
- プロトコル新規作成は **`creating-server-protocol` スキル**、テストは **`creating-server-tests` スキル**を参照すると定石が確認できる。
- 非 ASCII（日本語）を含むファイル編集時は AGENTS.md の「文字化け防止ワークフロー」に従う。

---

## Task 1: スキーマに ElectricToGearGenerator を追加し、型を自動生成する

**Files:**
- Modify: `VanillaSchema/blocks.yml`（ブロックタイプ enum と `when` ブロック）

> このタスクは `edit-schema` スキルの手順に従って行う。スキーマ編集後、SourceGenerator が `BlockTypeConst.ElectricToGearGenerator` と `ElectricToGearGeneratorBlockParam`（`OutputModes` 配列を含む）を自動生成する。

- [ ] **Step 1: ブロックタイプ enum に追加**

`VanillaSchema/blocks.yml` 内のブロックタイプ列挙（`GearToElectricGenerator` がある箇所、おおよそ 107 行目付近）に 1 行追加する:

```yaml
    - GearToElectricGenerator
    - ElectricToGearGenerator
```

- [ ] **Step 2: `when` ブロック定義を追加**

`GearToElectricGenerator` の `when` ブロック（おおよそ 745-759 行目）の直後に、対になる定義を追加する:

```yaml
- when: ElectricToGearGenerator
  type: object
  implementationInterface:
  - IGearConnectors
  properties:
  - key: teethCount
    type: integer
    default: 10
  - key: outputModes
    type: array
    items:
      type: object
      properties:
      - key: rpm
        type: number
        default: 0
      - key: torque
        type: number
        default: 0
      - key: requiredPower
        type: number
        default: 0
  - key: gear
    ref: gear
```

- [ ] **Step 3: SourceGenerator を起動して型を生成する**

`edit-schema` スキルの手順でジェネレーターをトリガーする（CompileRequester のタイムスタンプ更新 → Unity コンパイル）。その後:

Run: `uloop compile --project-path ./moorestech_client`
Expected: コンパイル成功。エラー 0。生成された `BlockTypeConst.ElectricToGearGenerator`（定数）と `ElectricToGearGeneratorBlockParam` 型（`int TeethCount`、`OutputModes`（配列）、`Gear`）が参照可能になる。

- [ ] **Step 4: 生成された型名を確認する**

Run: `grep -rn "ElectricToGearGenerator" moorestech_client/Assets/**/Mooresmaster.Model.BlocksModule* 2>/dev/null | head`
Expected: `BlockTypeConst.ElectricToGearGenerator` と `ElectricToGearGeneratorBlockParam`、配列要素型（例 `ElectricToGearGeneratorBlockParam.OutputModesElement`、プロパティ `Rpm` / `Torque` / `RequiredPower` は `double`）が見つかる。**配列要素型の正確な名前とプロパティ型をここで確定し、以降のタスクのコードを合わせる**（本 Plan は要素型を `OutputModesElement`、各値を `double` と仮定して書いている。差異があれば後続のキャストを調整する）。

- [ ] **Step 5: Commit**

```bash
git add VanillaSchema/blocks.yml
git commit -m "feat(schema): add ElectricToGearGenerator block type with outputModes table"
```

---

## Task 2: クライアント同期用の状態クラスを作る

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricToGear/ElectricToGearGeneratorBlockStateDetail.cs`

> `GearToElectricGeneratorBlockStateDetail` の対称形。`GearStateDetail` を継承し、可視化用に選択 index・電力充足率・消費電力を載せる。クライアントは MessagePack のキー一致で自動デシリアライズするため、別途登録は不要。

- [ ] **Step 1: 状態クラスを作成**

```csharp
using System;
using Game.EnergySystem;
using Game.Gear.Common;
using MessagePack;

namespace Game.Block.Blocks.ElectricToGear
{
    [Serializable]
    [MessagePackObject]
    public class ElectricToGearGeneratorBlockStateDetail : GearStateDetail
    {
        public const string BlockStateDetailKey = "ElectricToGearGenerator";

        [Key(7)] public int SelectedIndex { get; set; }
        [Key(8)] public float ElectricFulfillmentRate { get; set; }
        [Key(9)] public float ConsumedElectricPower { get; set; }

        public ElectricToGearGeneratorBlockStateDetail(
            bool isClockwise,
            RPM currentRpm,
            Torque currentTorque,
            int selectedIndex,
            float electricFulfillmentRate,
            ElectricPower consumedPower) :
            base(isClockwise, currentRpm.AsPrimitive(), currentTorque.AsPrimitive())
        {
            SelectedIndex = selectedIndex;
            ElectricFulfillmentRate = electricFulfillmentRate;
            ConsumedElectricPower = consumedPower.AsPrimitive();
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ElectricToGearGeneratorBlockStateDetail()
        {
        }
    }
}
```

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0（`GearStateDetail` の `[Key(0..2)]` と衝突しない 7/8/9 を使用）。

- [ ] **Step 3: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricToGear/ElectricToGearGeneratorBlockStateDetail.cs
git commit -m "feat: add ElectricToGearGeneratorBlockStateDetail for client sync"
```

---

## Task 3: 本体コンポーネントを実装する

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricToGear/ElectricToGearGeneratorComponent.cs`

> 核心。`GearEnergyTransformer` を継承（`consumption = null` でギアは消費しない）し、`IGearGenerator`・`IElectricConsumer`・`IBlockStateDetail`・`IBlockSaveState` を実装。RPM は選択固定、トルクは充足率でドループ、電力要求は選択エントリで固定。テストは Task 6 で TDD する（本タスクは実装、Task 6 がレッド→グリーンを回す）。

- [ ] **Step 1: コンポーネントを作成**

```csharp
using System;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Gear.Common;
using MessagePack;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.ElectricToGear
{
    public class ElectricToGearGeneratorComponent :
        GearEnergyTransformer, IGearGenerator, IElectricConsumer, IBlockStateDetail, IBlockSaveState
    {
        public int TeethCount => _param.TeethCount;
        public bool GenerateIsClockwise => true;

        // RPM は選択モードで固定。トルクは電力充足率でドループ。
        // RPM is fixed by the selected mode; torque droops by electric fulfillment.
        public RPM GenerateRpm => new RPM((float)CurrentMode.Rpm);
        public Torque GenerateTorque => new Torque((float)CurrentMode.Torque * _electricFulfillmentRate);

        public string SaveKey => "electricToGearGenerator";

        private readonly ElectricToGearGeneratorBlockParam _param;
        private int _selectedIndex;
        private ElectricPower _suppliedPower;
        private float _electricFulfillmentRate;

        // 選択中の出力エントリ。index は常に範囲内に保つ。
        // The currently selected output entry; index is always kept in range.
        private ElectricToGearGeneratorBlockParam.OutputModesElement CurrentMode => _param.OutputModes[_selectedIndex];

        public ElectricToGearGeneratorComponent(
            ElectricToGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent) :
            base(null, blockInstanceId, connectorComponent)
        {
            _param = param;
            _selectedIndex = 0;
            _suppliedPower = new ElectricPower(0);
            _electricFulfillmentRate = 0f;
        }

        // セーブ復元用コンストラクタ
        // Constructor used to restore from saved state
        public ElectricToGearGeneratorComponent(
            System.Collections.Generic.Dictionary<string, string> componentStates,
            ElectricToGearGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent) :
            this(param, blockInstanceId, connectorComponent)
        {
            if (componentStates != null && componentStates.TryGetValue(SaveKey, out var raw) && int.TryParse(raw, out var index))
            {
                _selectedIndex = ClampIndex(index);
            }
        }

        #region IElectricConsumer

        // 要求電力は選択エントリの requiredPower で固定（負荷にも RPM にも依存しない）
        // RequestEnergy is fixed at the selected entry's requiredPower (independent of load/RPM)
        public ElectricPower RequestEnergy => new ElectricPower((float)CurrentMode.RequiredPower);

        public void SupplyEnergy(ElectricPower power)
        {
            BlockException.CheckDestroy(this);

            _suppliedPower = power;
            var required = (float)CurrentMode.RequiredPower;
            _electricFulfillmentRate = required > 0f ? Math.Min(power.AsPrimitive() / required, 1f) : 0f;
        }

        #endregion

        // プロトコルから呼ばれる出力モード切替。範囲外 index は無視する。
        // Output-mode switch called by the protocol; out-of-range index is ignored.
        public void SetSelectedMode(int index)
        {
            BlockException.CheckDestroy(this);
            if (index < 0 || index >= _param.OutputModes.Length) return;
            _selectedIndex = index;
        }

        public int SelectedIndex => _selectedIndex;

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            return _selectedIndex.ToString();
        }

        public new BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);

            var baseDetails = base.GetBlockStateDetails();
            var result = new BlockStateDetail[baseDetails.Length + 1];
            result[0] = CreateDetail();
            Array.Copy(baseDetails, 0, result, 1, baseDetails.Length);
            return result;

            #region Internal

            BlockStateDetail CreateDetail()
            {
                var detail = new ElectricToGearGeneratorBlockStateDetail(
                    IsCurrentClockwise,
                    CurrentRpm,
                    CurrentTorque,
                    _selectedIndex,
                    _electricFulfillmentRate,
                    _suppliedPower);
                var serialized = MessagePackSerializer.Serialize(detail);
                return new BlockStateDetail(ElectricToGearGeneratorBlockStateDetail.BlockStateDetailKey, serialized);
            }

            #endregion
        }

        private int ClampIndex(int index)
        {
            if (index < 0) return 0;
            if (index >= _param.OutputModes.Length) return _param.OutputModes.Length - 1;
            return index;
        }
    }
}
```

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。`IBlockSaveState` / `IElectricConsumer` / `IGearGenerator` を全て満たす。`OutputModesElement` のプロパティ名（`Rpm`/`Torque`/`RequiredPower`）が Task 1 Step 4 の確認と一致していること。違えばここで修正する。

- [ ] **Step 3: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/ElectricToGear/ElectricToGearGeneratorComponent.cs
git commit -m "feat: add ElectricToGearGeneratorComponent (fixed RPM, torque droop, mode select)"
```

---

## Task 4: ブロックテンプレートを作り、登録する

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaElectricToGearGeneratorTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs`

> `VanillaGearToElectricGeneratorTemplate` を手本にしつつ、save/load 対応のため `componentStates` 分岐を入れる（`VanillaBeltConveyorTemplate` のパターン）。

- [ ] **Step 1: テンプレートを作成**

```csharp
using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.ElectricToGear;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaElectricToGearGeneratorTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Create(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Create(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private IBlock Create(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = blockMasterElement.BlockParam as ElectricToGearGeneratorBlockParam;
            var gearConnects = param.Gear.GearConnects;
            var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer>(gearConnects, gearConnects, blockPositionInfo);

            var component = componentStates == null
                ? new ElectricToGearGeneratorComponent(param, blockInstanceId, gearConnector)
                : new ElectricToGearGeneratorComponent(componentStates, param, blockInstanceId, gearConnector);

            var components = new List<IBlockComponent>
            {
                component,
                gearConnector,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
```

- [ ] **Step 2: テンプレートを登録**

`VanillaIBlockTemplates.cs` の `GearToElectricGenerator` 登録行（37 行目付近）の直後に追加:

```csharp
            BlockTypesDictionary.Add(BlockTypeConst.GearToElectricGenerator, new VanillaGearToElectricGeneratorTemplate());
            BlockTypesDictionary.Add(BlockTypeConst.ElectricToGearGenerator, new VanillaElectricToGearGeneratorTemplate());
```

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。

- [ ] **Step 4: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaElectricToGearGeneratorTemplate.cs moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs
git commit -m "feat: register VanillaElectricToGearGeneratorTemplate"
```

---

## Task 5: テスト用 Mod データと BlockId を追加する

**Files:**
- Modify: `.../forUnitTest/master/blocks.json`
- Modify: `.../forUnitTest/master/items.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs`

> GUID は空きを使用（確認済み: block `00000000-0000-0000-0000-000000000099`、item `11110009-0000-0000-0000-000000000000`）。**編集前に再度 `grep` で未使用を確認**してから使う。

- [ ] **Step 1: items.json にアイテムを追加**

`items.json` の配列末尾付近に追加（既存要素の体裁に合わせる）:

```json
{
  "itemGuid": "11110009-0000-0000-0000-000000000000",
  "imagePath": "",
  "name": "TestElectricToGearGenerator",
  "maxStack": 100,
  "sortPriority": 741,
  "initialUnlocked": true,
  "recipeViewType": "IsUnlocked",
  "handGrabModelAddressablePath": ""
}
```

- [ ] **Step 2: blocks.json にブロックを追加**

`blocks.json` の配列に追加。`outputModes` を 3 エントリ持たせる:

```json
{
  "blockGuid": "00000000-0000-0000-0000-000000000099",
  "itemGuid": "11110009-0000-0000-0000-000000000000",
  "name": "TestElectricToGearGenerator",
  "blockType": "ElectricToGearGenerator",
  "blockSize": [1, 1, 1],
  "blockPrefabAddressablesPath": "",
  "blockUIAddressablesPath": "",
  "blockParam": {
    "teethCount": 10,
    "outputModes": [
      { "rpm": 60, "torque": 50, "requiredPower": 30 },
      { "rpm": 120, "torque": 100, "requiredPower": 100 },
      { "rpm": 240, "torque": 150, "requiredPower": 300 }
    ],
    "gear": {
      "gearConnects": [
        {
          "offset": [0, 0, 0],
          "connectType": "Gear",
          "connectOption": { "isReverse": false },
          "directions": [[0, 0, -1], [0, 0, 1], [1, 0, 0], [-1, 0, 0]],
          "connectorGuid": "72231375-1dcb-4af7-8314-9eaa783116e9"
        }
      ]
    }
  },
  "overrideVerticalBlock": {}
}
```

> 注: `connectorGuid` は `TestGearToElectricGenerator` と同じ値を流用してよい（接続定義の参照用）。問題が出たら一意な GUID に差し替える。

- [ ] **Step 3: ForUnitTestModBlockId.cs に BlockId を追加**

`TestGearToElectricGenerator`（62 行目付近）の直後に追加:

```csharp
        public static BlockId TestGearToElectricGenerator => GetBlock("00000000-0000-0000-0000-000000000028");
        public static BlockId TestElectricToGearGenerator => GetBlock("00000000-0000-0000-0000-000000000099");
```

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0（JSON はコンパイル対象外だが、`ForUnitTestModBlockId.cs` の追加を確認）。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs
git commit -m "test: add TestElectricToGearGenerator mod data + block id"
```

---

## Task 6: コンポーネント挙動の単体テスト（TDD）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/ElectricToGearGeneratorTest.cs`

> `SupplyEnergy` を直接呼んで決定的に検証する（電力網を組まない）。RPM 固定・トルクドループ・モード切替・範囲外無視を確認。`creating-server-tests` スキルの命名・初期化に従う。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System;
using Core.Master;
using Game.Block.Blocks.ElectricToGear;
using Game.Context;
using Game.EnergySystem;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class ElectricToGearGeneratorTest
    {
        [Test]
        public void FixedRpmTorqueDroopsAndModeSwitch()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestElectricToGearGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var c = block.GetComponent<ElectricToGearGeneratorComponent>();
            var param = (ElectricToGearGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestElectricToGearGenerator).BlockParam;

            var mode0 = param.OutputModes[0]; // rpm 60, torque 50, power 30
            var mode1 = param.OutputModes[1]; // rpm 120, torque 100, power 100

            // 既定は index 0。要求電力は mode0.requiredPower、RPM は mode0.rpm 固定。
            // Default index 0: request = mode0.requiredPower, RPM fixed at mode0.rpm.
            Assert.AreEqual((float)mode0.RequiredPower, c.RequestEnergy.AsPrimitive(), 0.001f);
            Assert.AreEqual((float)mode0.Rpm, c.GenerateRpm.AsPrimitive(), 0.001f);

            // フル供給 → トルクは選択値、RPM 固定。
            // Full supply → torque at selected value, RPM fixed.
            c.SupplyEnergy(new ElectricPower((float)mode0.RequiredPower));
            Assert.AreEqual((float)mode0.Torque, c.GenerateTorque.AsPrimitive(), 0.01f);
            Assert.AreEqual((float)mode0.Rpm, c.GenerateRpm.AsPrimitive(), 0.001f);

            // 半分供給 → トルク半減、RPM は変わらない（ドループ）。
            // Half supply → torque halves, RPM unchanged (droop).
            c.SupplyEnergy(new ElectricPower((float)mode0.RequiredPower * 0.5f));
            Assert.AreEqual((float)mode0.Torque * 0.5f, c.GenerateTorque.AsPrimitive(), 0.01f);
            Assert.AreEqual((float)mode0.Rpm, c.GenerateRpm.AsPrimitive(), 0.001f);

            // 電力ゼロ → トルク 0。
            // Zero supply → torque 0.
            c.SupplyEnergy(new ElectricPower(0));
            Assert.AreEqual(0f, c.GenerateTorque.AsPrimitive(), 0.01f);

            // モード切替 → 要求電力と RPM が mode1 に即切替。
            // Mode switch → request and RPM jump to mode1.
            c.SetSelectedMode(1);
            Assert.AreEqual((float)mode1.RequiredPower, c.RequestEnergy.AsPrimitive(), 0.001f);
            Assert.AreEqual((float)mode1.Rpm, c.GenerateRpm.AsPrimitive(), 0.001f);

            // 範囲外 index は無視（mode1 のまま）。
            // Out-of-range index ignored (stays at mode1).
            c.SetSelectedMode(99);
            Assert.AreEqual(1, c.SelectedIndex);
            c.SetSelectedMode(-1);
            Assert.AreEqual(1, c.SelectedIndex);
        }
    }
}
```

- [ ] **Step 2: Unity を再起動してからテストを実行（新規テスト .cs のため）**

memory `server-tests-immutable-package` に従い、新規テストファイルは Unity 再起動が必要。`uloop-launch` スキルで再起動後:

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricToGearGeneratorTest"`
Expected: 実装（Task 3）が正しければ PASS。失敗時はメッセージを読み、`OutputModesElement` のプロパティ型キャストや充足率計算を見直す。

- [ ] **Step 3: Commit**

```bash
git add moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/ElectricToGearGeneratorTest.cs
git commit -m "test: ElectricToGearGenerator fixed-RPM torque-droop and mode switch"
```

---

## Task 7: save/load ラウンドトリップのテスト

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/ElectricToGearGeneratorTest.cs`（テストメソッド追加）

> `GetSaveState()` の文字列を `componentStates` 辞書に詰め、テンプレートの `Load` 経路で復元して `selectedIndex` が戻ることを確認する。

- [ ] **Step 1: save/load テストを追加**

`ElectricToGearGeneratorTest` クラス内に追加:

```csharp
        [Test]
        public void SelectedIndexSurvivesSaveLoad()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.TestElectricToGearGenerator, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var c = block.GetComponent<ElectricToGearGeneratorComponent>();

            // モードを 2 に変更し、ブロックの全コンポーネント状態をセーブする。
            // Switch to mode 2 and save the block's full component state.
            c.SetSelectedMode(2);
            var saved = block.GetSaveState(); // Dictionary<string,string> 相当（既存 IBlock のセーブ API）

            // 同じ座標に置き直すためいったん撤去し、セーブ状態からロードする。
            // Remove and re-create the block at the same position, loading from saved state.
            world.RemoveBlock(Vector3Int.zero);
            world.TryAddLoadedBlock(ForUnitTestModBlockId.TestElectricToGearGenerator, Vector3Int.zero, BlockDirection.North, saved, out var reloaded);
            var rc = reloaded.GetComponent<ElectricToGearGeneratorComponent>();

            Assert.AreEqual(2, rc.SelectedIndex);
        }
```

> 注: `block.GetSaveState()`（ブロック単位のコンポーネント状態取得）と `world.TryAddLoadedBlock(...)`（セーブ状態からの復元配置）の正確な API 名は、既存の save/load テスト（例: `FilterSplitter` や belt の save テスト）を `grep "GetSaveState\|AddLoadedBlock\|LoadBlock"` で確認し、合わせる。シグネチャが異なる場合はそのプロジェクトの定石に従って呼び出しを差し替える。狙いは「mode 2 を選んで save→load し、index 2 が復元される」こと。

- [ ] **Step 2: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricToGearGeneratorTest"`
Expected: 2 テストとも PASS。

- [ ] **Step 3: Commit**

```bash
git add moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/ElectricToGearGeneratorTest.cs
git commit -m "test: ElectricToGearGenerator selectedIndex survives save/load"
```

---

## Task 8: 出力モード切替プロトコルを実装する

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/SetElectricToGearOutputModeProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`

> `SetTrainPlatformTransferModeProtocol` を手本に、`creating-server-protocol` スキルの定石で実装する。

- [ ] **Step 1: プロトコルを作成**

```csharp
using System;
using Game.Block.Blocks.ElectricToGear;
using Game.Context;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class SetElectricToGearOutputModeProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:setElectricToGearOutputMode";

        public SetElectricToGearOutputModeProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<SetElectricToGearOutputModeRequest>(payload);

            // 指定座標のブロックを取得
            // Fetch the block at the requested position
            var block = ServerContext.WorldBlockDatastore.GetBlock(request.Position.Vector3Int);
            if (block == null)
            {
                return new SetElectricToGearOutputModeResponse(false, request.Index);
            }

            // ElectricToGear コンポーネントを取り出し、モードを切り替える
            // Fetch the ElectricToGear component and switch the mode
            if (!block.ComponentManager.TryGetComponent<ElectricToGearGeneratorComponent>(out var component))
            {
                return new SetElectricToGearOutputModeResponse(false, request.Index);
            }

            component.SetSelectedMode(request.Index);
            return new SetElectricToGearOutputModeResponse(true, component.SelectedIndex);
        }
    }

    [MessagePackObject]
    public class SetElectricToGearOutputModeRequest : ProtocolMessagePackBase
    {
        [Key(2)] public Vector3IntMessagePack Position { get; set; }
        [Key(3)] public int Index { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public SetElectricToGearOutputModeRequest()
        {
        }

        public SetElectricToGearOutputModeRequest(Vector3Int position, int index)
        {
            Tag = SetElectricToGearOutputModeProtocol.ProtocolTag;
            Position = new Vector3IntMessagePack(position);
            Index = index;
        }
    }

    [MessagePackObject]
    public class SetElectricToGearOutputModeResponse : ProtocolMessagePackBase
    {
        [Key(2)] public bool Success { get; set; }
        [Key(3)] public int AppliedIndex { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public SetElectricToGearOutputModeResponse()
        {
        }

        public SetElectricToGearOutputModeResponse(bool success, int appliedIndex)
        {
            Tag = SetElectricToGearOutputModeProtocol.ProtocolTag;
            Success = success;
            AppliedIndex = appliedIndex;
        }
    }
}
```

> 注: `Vector3IntMessagePack` の名前空間（`Server.Util.MessagePack`）と `ProtocolMessagePackBase` / `PacketResponseContext` / `ServiceProvider` の using は、`SetTrainPlatformTransferModeProtocol.cs` の using をそのまま確認して合わせる。

- [ ] **Step 2: プロトコルを登録**

`PacketResponseCreator.cs` の ctor 内（他の `_packetResponseDictionary.Add(...)` が並ぶ箇所、30 行目付近）に追加:

```csharp
            _packetResponseDictionary.Add(SetElectricToGearOutputModeProtocol.ProtocolTag, new SetElectricToGearOutputModeProtocol(serviceProvider));
```

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。

- [ ] **Step 4: Commit**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/SetElectricToGearOutputModeProtocol.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs
git commit -m "feat: add SetElectricToGearOutputModeProtocol"
```

---

## Task 9: プロトコルのパケットテスト

**Files:**
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricToGearOutputModeProtocolTest.cs`

> `FilterSplitterStateProtocolTest` を手本に、パケット送信 → ブロックのモードが変わることを確認する。

- [ ] **Step 1: パケットテストを書く**

```csharp
using System;
using Game.Block.Blocks.ElectricToGear;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class ElectricToGearOutputModeProtocolTest
    {
        private static readonly Vector3Int Pos = Vector3Int.zero;

        [Test]
        public void SetOutputModeViaPacket()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestElectricToGearGenerator, Pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var c = block.GetComponent<ElectricToGearGeneratorComponent>();

            // index 2 に切替えるリクエストを送る。
            // Send a request to switch to index 2.
            var response = Send(packet, new SetElectricToGearOutputModeRequest(Pos, 2));

            Assert.IsTrue(response.Success);
            Assert.AreEqual(2, response.AppliedIndex);
            Assert.AreEqual(2, c.SelectedIndex);

            // 範囲外 index は無視され、現状維持。
            // Out-of-range index is ignored; state preserved.
            var response2 = Send(packet, new SetElectricToGearOutputModeRequest(Pos, 99));
            Assert.IsTrue(response2.Success);
            Assert.AreEqual(2, response2.AppliedIndex);
            Assert.AreEqual(2, c.SelectedIndex);
        }

        #region Internal

        private static SetElectricToGearOutputModeResponse Send(PacketResponseCreator packet, SetElectricToGearOutputModeRequest request)
        {
            var payload = MessagePackSerializer.Serialize(request);
            var responseBytes = packet.GetPacketResponse(payload, new PacketResponseContext())[0];
            return MessagePackSerializer.Deserialize<SetElectricToGearOutputModeResponse>(responseBytes);
        }

        #endregion
    }
}
```

> 注: `new MoorestechServerDIContainerGenerator().Create(...)` の戻りタプルの形（`(packet, _)`）と `PacketResponseContext` の using は `FilterSplitterStateProtocolTest.cs` を確認して合わせる。

- [ ] **Step 2: Unity 再起動 → テスト実行**

新規テスト .cs のため Unity を再起動してから:

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricToGearOutputModeProtocolTest"`
Expected: PASS。

- [ ] **Step 3: Commit**

```bash
git add moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricToGearOutputModeProtocolTest.cs
git commit -m "test: ElectricToGearOutputModeProtocol packet test"
```

---

## Task 10: クライアントの送電範囲表示に追加する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Electric/DisplayEnergizedRange.cs:133`

> このブロックは電力消費者なので、送電範囲ハイライト対象に含める（GearToElectric と同様）。純粋なコード編集。

- [ ] **Step 1: enum 判定に追加**

133 行目の条件に `BlockTypeConst.ElectricToGearGenerator` を追加:

```csharp
                return type is BlockTypeConst.ElectricGenerator or BlockTypeConst.ElectricMachine or BlockTypeConst.ElectricMiner or BlockTypeConst.GearToElectricGenerator or BlockTypeConst.ElectricPump or BlockTypeConst.ElectricToGearGenerator;
```

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。

- [ ] **Step 3: 全テスト回帰確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricToGear|GearToElectric"`
Expected: 既存の GearToElectric テストと新規 ElectricToGear テストが全て PASS。

- [ ] **Step 4: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Electric/DisplayEnergizedRange.cs
git commit -m "feat(client): include ElectricToGearGenerator in energized-range display"
```

---

## Out of Scope（別 Plan で実施）

- **Plan 2: クライアントのモード選択 UI。** プレイヤーがブロック UI で `outputModes` の 1 行を選び、`SetElectricToGearOutputModeProtocol` を送る UI。`ElectricToGearGeneratorBlockStateDetail.SelectedIndex` を読んで現在モードを表示。Unity prefab/UI 作業を含むため別ツーリング（`uloop execute-dynamic-code` か手動）。
- **実プレイ用 Mod データ・アイテム画像。** `../moorestech_master` の `blocks.json` / `items.json` 追加と画像アセット。`master-asset-converter` / sortPriority 再計算系スキルを使用。
- **ブロック見た目（prefab、ギア回転アニメ）。** `ElectricToGearGeneratorBlockStateDetail` の `CurrentRpm` / `SelectedIndex` を使った回転表現。

---

## Self-Review メモ（計画作成者による確認）

- **Spec カバレッジ:** 出力テーブル（Task 1, 5）／RPM 固定・トルクドループ（Task 3, 6）／要求電力固定（Task 3, 6）／requiredPower 明示（Task 1, 5）／ランタイム選択（Task 8, 9）／選択の永続化（Task 3, 7）／クライアント同期状態（Task 2）／送電範囲表示（Task 10）。クライアント選択 UI は Out of Scope に明記。
- **型整合:** `SetSelectedMode(int)` / `SelectedIndex`（int getter）/ `RequestEnergy` / `GenerateRpm` / `GenerateTorque` は Task 3 定義と Task 6/8/9 の参照で一致。状態キー `"ElectricToGearGenerator"`、SaveKey `"electricToGearGenerator"`、ProtocolTag `"va:setElectricToGearOutputMode"`。
- **要確認（実装時）:** 生成配列要素型名（`OutputModesElement` 仮定, Task 1 Step 4）／`number`→`double` キャスト／save-load の `GetSaveState`・`TryAddLoadedBlock` 系 API 名（Task 7 注記）／プロトコル基底クラスの using（Task 8 注記）。いずれも該当タスク内に確認手順を明記済み。
