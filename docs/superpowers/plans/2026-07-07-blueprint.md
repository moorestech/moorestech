# ブループリント（建築コピペ）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 矩形範囲の既存建築を名前付きでサーバーセーブに保存し、一覧から選んで回転・貼り付けできるブループリント機能を実装する。

**Architecture:** サーバーに新asmdef `Game.Blueprint`（データモデル・Datastore・抽出/展開の純関数）を追加し、`WorldSaveAllInfoV1` へ永続化統合。通信は単一プロトコル `va:blueprint`（Operation分岐）。貼り付けは既存 `va:placeBlock` に `BlockCreateParam`（設定JSONのUTF8バイト）を載せて送る。クライアントは `PlacementSelectionType` 拡張＋新 `IPlaceSystem` 2種（コピー/貼り付け）で既存の選択→設置フローに統合する。

**Tech Stack:** Unity / C# / MessagePack（通信）/ Newtonsoft.Json（永続化）/ UniRx / uloop CLI

**Spec:** `docs/superpowers/specs/2026-07-07-blueprint-design.md`

## Global Constraints

- 1ファイル200行以下。超える場合はディレクトリ分割（1ディレクトリ10ファイルまで）。**partial は如何なる条件でも禁止**
- デフォルト引数禁止。try-catch 禁止。イベントは UniRx（`event Action` 禁止）
- 単純getter/setterプロパティ禁止、Setは `public void SetHoge` メソッド
- コメントは日本語・英語の2行セット（各1行、3〜10行ごと）。自明なコメントは書かない
- 永続化は可読JSON・GUID保存（揮発int禁止・マスタ由来値保存禁止・MessagePack禁止）。通信はMessagePack
- .cs変更後は必ず `uloop compile --project-path ./moorestech_client` を実行
- .metaファイル手動作成禁止。Prefab/Scene直接編集禁止（`uloop execute-dynamic-code` 経由のみ可）
- テスト実行: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "正規表現"`（サーバーテストもクライアントproject-pathで実行）
- 作業開始時に必ず `pwd` で現在ディレクトリを確認。タスク終了ごとに必ずコミット
- ドメインリロードエラー（"Unity is reloading"）が出たら45秒待ってリトライ

## 配置と前例（spec-architecture-review済み）

| 配置決定 | 前例 |
|---|---|
| 新asmdef `Game.Blueprint`（Datastore+JsonObject+純関数サービス） | `Game.UnlockState/Game.UnlockState.asmdef`（軽量参照構成） |
| Datastoreは `ServerContext` 静的公開せずDI singleton | `services.AddSingleton<IResearchDataStore, ResearchDataStore>()`（MoorestechServerDIContainerGenerator.cs:161） |
| セーブ統合は `WorldSaveAllInfoV1`+`AssembleSaveJsonText`+`WorldLoaderFromJson` の3点 | GameUnlockState/Research の統合形 |
| `IBlockBlueprintSettings` は `Game.Block.Interface/Component/` | `IBlockSaveState.cs`（同ディレクトリ） |
| 単一プロトコル+Operation enum+static factory | `FilterSplitterStateProtocol.cs` |
| VanillaApiは1プロトコル=1メソッド | `SendFilterSplitterStateRequest`（VanillaApiWithResponse.cs:314） |
| BlockCreateParam消費はテンプレートのNew内 | `VanillaTrainRailTemplate.cs:37`（`GetStateDetail<T>`） |
| 貼り付け展開計算（回転）はサーバー側 `Game.Blueprint` の純関数（クライアントはサーバーasmdefを直接参照できるため共用） | クライアントが `PlaceInfo`（Server.Protocol）を直接使用している前例 |

---

### Task 1: Game.Blueprint モジュール（データモデル＋Datastore＋セーブ統合）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Blueprint/Game.Blueprint.asmdef`
- Create: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintJsonObject.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Blueprint/IBlueprintDatastore.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Game.SaveLoad.asmdef`（references に `Game.Blueprint` 追加。既存参照形式に合わせる）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintDatastoreTest.cs`

**Interfaces:**
- Produces: `IBlueprintDatastore`（`string Register(BlueprintJsonObject)` / `IReadOnlyList<BlueprintJsonObject> Blueprints` / `bool Delete(string name)` / `List<BlueprintJsonObject> GetSaveJsonObject()` / `void LoadBlueprints(List<BlueprintJsonObject>)`）、`BlueprintJsonObject`（`Name`, `Blocks`）、`BlueprintBlockJsonObject`（`OffsetX/Y/Z`, `BlockGuidStr`, `Direction`, `Settings`）

- [ ] **Step 1: pwd確認とasmdef作成**

Run: `pwd`（`/Users/katsumi/moorestech` であること）

`Game.Blueprint.asmdef`:
```json
{
    "name": "Game.Blueprint",
    "rootNamespace": "",
    "references": [
        "Core.Master",
        "Game.Block.Interface",
        "Game.World.Interface"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: データモデルを作成**

`BlueprintJsonObject.cs`:
```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Blueprint
{
    public class BlueprintJsonObject
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("blocks")] public List<BlueprintBlockJsonObject> Blocks;

        public BlueprintJsonObject()
        {
            Blocks = new List<BlueprintBlockJsonObject>();
        }

        public BlueprintJsonObject(string name, List<BlueprintBlockJsonObject> blocks)
        {
            Name = name;
            Blocks = blocks;
        }
    }

    public class BlueprintBlockJsonObject
    {
        // アンカー（選択矩形XZ中心・最下段Y）からの相対オフセット
        // Offset relative to the anchor (rect XZ center, lowest Y)
        [JsonProperty("offsetX")] public int OffsetX;
        [JsonProperty("offsetY")] public int OffsetY;
        [JsonProperty("offsetZ")] public int OffsetZ;

        [JsonProperty("blockGuid")] public string BlockGuidStr;
        [JsonIgnore] public Guid BlockGuid => Guid.Parse(BlockGuidStr);

        [JsonProperty("direction")] public int Direction;

        // 設定キー→設定JSON（可読形式）。実行時状態は含まない
        // Settings key to readable settings JSON; runtime state excluded
        [JsonProperty("settings")] public Dictionary<string, string> Settings;

        [JsonIgnore] public Vector3Int Offset => new(OffsetX, OffsetY, OffsetZ);

        public BlueprintBlockJsonObject()
        {
            Settings = new Dictionary<string, string>();
        }

        public BlueprintBlockJsonObject(Vector3Int offset, string blockGuidStr, int direction, Dictionary<string, string> settings)
        {
            OffsetX = offset.x;
            OffsetY = offset.y;
            OffsetZ = offset.z;
            BlockGuidStr = blockGuidStr;
            Direction = direction;
            Settings = settings;
        }
    }
}
```

- [ ] **Step 3: インターフェースと実装を作成**

`IBlueprintDatastore.cs`:
```csharp
using System.Collections.Generic;

namespace Game.Blueprint
{
    public interface IBlueprintDatastore
    {
        IReadOnlyList<BlueprintJsonObject> Blueprints { get; }

        // 重複名は連番付与するため、確定した登録名を返す
        // Returns the final registered name after duplicate-suffixing
        string Register(BlueprintJsonObject blueprint);
        bool Delete(string name);

        List<BlueprintJsonObject> GetSaveJsonObject();
        void LoadBlueprints(List<BlueprintJsonObject> blueprints);
    }
}
```

`BlueprintDatastore.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;

namespace Game.Blueprint
{
    public class BlueprintDatastore : IBlueprintDatastore
    {
        private readonly List<BlueprintJsonObject> _blueprints = new();

        public IReadOnlyList<BlueprintJsonObject> Blueprints => _blueprints;

        public string Register(BlueprintJsonObject blueprint)
        {
            // 重複名には " (2)" 形式の連番を付与して常に登録成功させる
            // Suffix duplicates with " (2)" style numbering so register always succeeds
            var name = blueprint.Name;
            var suffix = 2;
            while (_blueprints.Any(b => b.Name == name))
            {
                name = $"{blueprint.Name} ({suffix})";
                suffix++;
            }

            blueprint.Name = name;
            _blueprints.Add(blueprint);
            return name;
        }

        public bool Delete(string name)
        {
            var target = _blueprints.FirstOrDefault(b => b.Name == name);
            if (target == null) return false;

            _blueprints.Remove(target);
            return true;
        }

        public List<BlueprintJsonObject> GetSaveJsonObject()
        {
            return new List<BlueprintJsonObject>(_blueprints);
        }

        public void LoadBlueprints(List<BlueprintJsonObject> blueprints)
        {
            _blueprints.Clear();
            _blueprints.AddRange(blueprints);
        }
    }
}
```

- [ ] **Step 4: セーブ統合（3ファイル＋DI＋asmdef）**

`WorldSaveAllInfoV1.cs` — コンストラクタ引数の末尾に `List<BlueprintJsonObject> blueprints` を追加し、代入とプロパティを追加（using に `Game.Blueprint` 追加）:
```csharp
// コンストラクタ引数末尾に追加 / Add to constructor parameter list tail
List<BlueprintJsonObject> blueprints)
// コンストラクタ内の代入に追加 / Add assignment in constructor
Blueprints = blueprints ?? new List<BlueprintJsonObject>();
// プロパティ群の末尾に追加 / Add to property list tail
[JsonProperty("blueprints")] public List<BlueprintJsonObject> Blueprints { get; set; }
```

`AssembleSaveJsonText.cs` — コンストラクタに `IBlueprintDatastore blueprintDatastore` を注入しフィールド保持、`new WorldSaveAllInfoV1(...)` の実引数末尾に `_blueprintDatastore.GetSaveJsonObject()` を追加。

`WorldLoaderFromJson.cs` — コンストラクタに `IBlueprintDatastore blueprintDatastore` を注入しフィールド保持、`Load()` の末尾に追加:
```csharp
_blueprintDatastore.LoadBlueprints(load.Blueprints ?? new List<BlueprintJsonObject>());
```

`MoorestechServerDIContainerGenerator.cs` — `services.AddSingleton<IResearchDataStore, ResearchDataStore>();` の直後に追加:
```csharp
services.AddSingleton<IBlueprintDatastore, BlueprintDatastore>();
```

`Game.SaveLoad.asmdef` と `Server.Boot` のasmdef（DI Generator所属）の references に `Game.Blueprint` を追加（既存の参照が GUID 形式の場合は Unity 起動後に Editor 経由で追加するか、既存が名前形式ならそのまま追記）。

- [ ] **Step 5: 失敗するテストを書く**

`Tests/CombinedTest/Game/BlueprintDatastoreTest.cs`:
```csharp
using System.Collections.Generic;
using Game.Blueprint;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class BlueprintDatastoreTest
    {
        [Test]
        public void RegisterAndDuplicateNameTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<IBlueprintDatastore>();

            // 同名登録で連番が付与されることを確認
            // Duplicate names get numbered suffixes
            var name1 = datastore.Register(CreateBlueprint("factory"));
            var name2 = datastore.Register(CreateBlueprint("factory"));
            var name3 = datastore.Register(CreateBlueprint("factory"));

            Assert.AreEqual("factory", name1);
            Assert.AreEqual("factory (2)", name2);
            Assert.AreEqual("factory (3)", name3);
            Assert.AreEqual(3, datastore.Blueprints.Count);

            #region Internal

            BlueprintJsonObject CreateBlueprint(string name)
            {
                var block = new BlueprintBlockJsonObject(Vector3Int.zero, System.Guid.NewGuid().ToString(), 0, new Dictionary<string, string>());
                return new BlueprintJsonObject(name, new List<BlueprintBlockJsonObject> { block });
            }

            #endregion
        }

        [Test]
        public void DeleteTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<IBlueprintDatastore>();

            datastore.Register(new BlueprintJsonObject("target", new List<BlueprintBlockJsonObject>()));

            Assert.IsTrue(datastore.Delete("target"));
            Assert.AreEqual(0, datastore.Blueprints.Count);
            Assert.IsFalse(datastore.Delete("missing"));
        }

        [Test]
        public void SaveLoadRoundTripTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<IBlueprintDatastore>();

            var settings = new Dictionary<string, string> { { "TestKey", "{\"a\":1}" } };
            var block = new BlueprintBlockJsonObject(new Vector3Int(1, 0, -2), System.Guid.NewGuid().ToString(), 3, settings);
            datastore.Register(new BlueprintJsonObject("roundtrip", new List<BlueprintBlockJsonObject> { block }));

            // セーブJSONを取り出し、別Datastoreに復元して一致を確認
            // Extract save JSON and restore into a fresh datastore
            var saved = datastore.GetSaveJsonObject();
            var restored = new BlueprintDatastore();
            restored.LoadBlueprints(saved);

            Assert.AreEqual(1, restored.Blueprints.Count);
            var restoredBlock = restored.Blueprints[0].Blocks[0];
            Assert.AreEqual(new Vector3Int(1, 0, -2), restoredBlock.Offset);
            Assert.AreEqual(3, restoredBlock.Direction);
            Assert.AreEqual("{\"a\":1}", restoredBlock.Settings["TestKey"]);
        }
    }
}
```

- [ ] **Step 6: コンパイルしてテスト失敗を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（テスト以外）。テスト実行:
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintDatastoreTest"`
Expected: Step 1-4 実装済みならPASS（TDD順序上、先にテストだけ書いてコンパイルエラーを確認してから実装してもよい）

- [ ] **Step 7: 既存セーブ系テストのデグレ確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveLoad|AssembleSaveJsonTextTest"`
Expected: 全PASS（`WorldSaveAllInfoV1` のコンストラクタ引数追加でコンパイルエラーになった既存呼び出しはこの時点で全部直っていること）

- [ ] **Step 8: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Blueprint moorestech_server/Assets/Scripts/Game.SaveLoad moorestech_server/Assets/Scripts/Server.Boot moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintDatastoreTest.cs
git commit -m "feat: Game.Blueprintモジュール追加（Datastore・セーブ統合・DI登録）"
```
（Unity起動で生成された.metaは含めてよい）

---

### Task 2: 範囲抽出サービス（BlueprintCreateService）＋ IBlockBlueprintSettings

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/IBlockBlueprintSettings.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintCreateService.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintCreateServiceTest.cs`

**Interfaces:**
- Consumes: `IBlueprintDatastore`, `BlueprintJsonObject`（Task 1）
- Produces: `IBlockBlueprintSettings`（`string BlueprintSettingsKey { get; }` / `string GetBlueprintSettingsJson()`）、`BlueprintCreateService.TryCreateFromArea(string name, int minX, int minZ, int maxX, int maxZ, out BlueprintJsonObject blueprint)`（static、範囲内対象0なら false）

- [ ] **Step 1: インターフェースを作成**

`IBlockBlueprintSettings.cs`:
```csharp
namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     ブループリントでコピー可能な「設定」を提供するコンポーネント
    ///     実行時状態（インベントリ・加工進捗）は含めないこと
    ///     Provides copyable "settings" for blueprints; exclude runtime state
    /// </summary>
    public interface IBlockBlueprintSettings : IBlockComponent
    {
        string BlueprintSettingsKey { get; }

        // 可読JSON。アイテム等の参照はGUID文字列で表現する
        // Readable JSON; represent item references as GUID strings
        string GetBlueprintSettingsJson();
    }
}
```

- [ ] **Step 2: 抽出サービスを作成**

`BlueprintCreateService.cs`:
```csharp
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using UnityEngine;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Game.Blueprint
{
    public static class BlueprintCreateService
    {
        // レール系はブロック外ドメイン（RailSegments）を持つためコピー対象外
        // Rail-family blocks are excluded; their graph lives outside block states
        private static readonly HashSet<string> ExcludedBlockTypes = new()
        {
            BlockTypeConst.TrainRail,
            BlockTypeConst.TrainStation,
            BlockTypeConst.TrainItemPlatform,
            BlockTypeConst.TrainFluidPlatform,
        };

        public static bool TryCreateFromArea(string name, int minX, int minZ, int maxX, int maxZ, out BlueprintJsonObject blueprint)
        {
            var targets = CollectTargets();
            if (targets.Count == 0)
            {
                blueprint = null;
                return false;
            }

            var anchor = CalcAnchor();
            var blocks = new List<BlueprintBlockJsonObject>();
            foreach (var data in targets)
            {
                blocks.Add(CreateBlockJson(data, anchor));
            }

            blueprint = new BlueprintJsonObject(name, blocks);
            return true;

            #region Internal

            List<global::Game.World.Interface.DataStore.WorldBlockData> CollectTargets()
            {
                var result = new List<global::Game.World.Interface.DataStore.WorldBlockData>();
                foreach (var data in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
                {
                    var master = MasterHolder.BlockMaster.GetBlockMaster(data.Block.BlockId);
                    if (ExcludedBlockTypes.Contains(master.BlockType)) continue;
                    if (!IntersectsRect(data)) continue;
                    result.Add(data);
                }

                return result;
            }

            // 占有セルのいずれかがXZ矩形に入っていれば対象（Yは全域）
            // Included when any occupied cell intersects the XZ rect; Y unbounded
            bool IntersectsRect(global::Game.World.Interface.DataStore.WorldBlockData data)
            {
                foreach (var pos in data.Block.BlockPositionInfo.EnumeratePositions())
                {
                    if (pos.x >= minX && pos.x <= maxX && pos.z >= minZ && pos.z <= maxZ) return true;
                }

                return false;
            }

            // アンカー = 矩形XZ中心セル、Y = 対象ブロック群の最下段
            // Anchor: XZ center cell of rect; Y is the lowest included origin
            Vector3Int CalcAnchor()
            {
                var minY = int.MaxValue;
                foreach (var data in targets)
                {
                    if (data.Block.BlockPositionInfo.OriginalPos.y < minY) minY = data.Block.BlockPositionInfo.OriginalPos.y;
                }

                return new Vector3Int((minX + maxX) / 2, minY, (minZ + maxZ) / 2);
            }

            BlueprintBlockJsonObject CreateBlockJson(global::Game.World.Interface.DataStore.WorldBlockData data, Vector3Int anchorPos)
            {
                var master = MasterHolder.BlockMaster.GetBlockMaster(data.Block.BlockId);
                var offset = data.Block.BlockPositionInfo.OriginalPos - anchorPos;
                var direction = (int)data.Block.BlockPositionInfo.BlockDirection;

                // 設定を持つコンポーネントからJSONを収集する
                // Collect settings JSON from settings-providing components
                var settings = new Dictionary<string, string>();
                foreach (var component in data.Block.ComponentManager.GetComponents<IBlockBlueprintSettings>())
                {
                    settings.Add(component.BlueprintSettingsKey, component.GetBlueprintSettingsJson());
                }

                return new BlueprintBlockJsonObject(offset, master.BlockGuid.ToString(), direction, settings);
            }

            #endregion
        }
    }
}
```
注意: `WorldBlockData` の名前空間が上記と異なる場合は実際の定義（`Game.World.Interface/DataStore/`）に合わせて using を整理する。`ComponentManager.GetComponents<T>` が無い場合は `BlockSystem.GetSaveState()` が使っている実APIに合わせる。

- [ ] **Step 3: 失敗するテストを書く**

`Tests/CombinedTest/Game/BlueprintCreateServiceTest.cs`:
```csharp
using System;
using System.Linq;
using Game.Block.Interface;
using Game.Blueprint;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class BlueprintCreateServiceTest
    {
        [Test]
        public void AreaExtractionTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 範囲内2つ・範囲外1つ・高さ違い1つを設置
            // Two blocks inside, one outside, one at a different height
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(3, 0, 4), BlockDirection.East, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(100, 0, 100), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(2, 5, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var created = BlueprintCreateService.TryCreateFromArea("test", 0, 0, 5, 5, out var blueprint);

            Assert.IsTrue(created);
            // 高さ全域が対象のため範囲内3つが含まれる
            // Full-height column selection includes all three in-rect blocks
            Assert.AreEqual(3, blueprint.Blocks.Count);

            // アンカーは矩形XZ中心(2, minY=0, 2)。原点(0,0,0)のオフセットは(-2,0,-2)
            // Anchor is rect XZ center (2, minY=0, 2)
            var chestBlock = blueprint.Blocks.First(b => b.Offset == new Vector3Int(-2, 0, -2));
            Assert.AreEqual((int)BlockDirection.North, chestBlock.Direction);

            var machineBlock = blueprint.Blocks.First(b => b.Offset == new Vector3Int(1, 0, 2));
            Assert.AreEqual((int)BlockDirection.East, machineBlock.Direction);

            var elevated = blueprint.Blocks.First(b => b.Offset == new Vector3Int(0, 5, 0));
            Assert.NotNull(elevated);
        }

        [Test]
        public void EmptyAreaReturnsFalseTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var created = BlueprintCreateService.TryCreateFromArea("empty", 0, 0, 5, 5, out var blueprint);

            Assert.IsFalse(created);
            Assert.IsNull(blueprint);
        }
    }
}
```
（テスト用マスタにレール系ブロックIDが定義済みなら、レール除外のテストケースも追加する。`ForUnitTestModBlockId` に無ければスキップし、除外ロジックはBlockType文字列比較のため実装レビューで担保）

- [ ] **Step 4: コンパイル＋テスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintCreateServiceTest"`
Expected: 全PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Block.Interface/Component/IBlockBlueprintSettings.cs moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintCreateService.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintCreateServiceTest.cs
git commit -m "feat: ブループリント範囲抽出サービスとIBlockBlueprintSettings追加"
```

---

### Task 3: 貼り付け展開計算（BlueprintPasteCalculator、回転含む）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintPasteCalculator.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintPlacementElement.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintPasteCalculatorTest.cs`

**Interfaces:**
- Consumes: `BlueprintJsonObject` / `BlueprintBlockJsonObject`（Task 1）
- Produces: `BlueprintPasteCalculator.CalculatePlacements(BlueprintJsonObject blueprint, Vector3Int pasteAnchor, int rotationStep)` → `List<BlueprintPlacementElement>`。`BlueprintPlacementElement`（`Vector3Int Position` / `BlockDirection Direction` / `BlockId BlockId` / `Dictionary<string, string> Settings`）。rotationStep は 0〜3（90度単位・時計回り）。マスタに存在しないGuidのブロックはスキップ

- [ ] **Step 1: 結果要素クラスを作成**

`BlueprintPlacementElement.cs`:
```csharp
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Blueprint
{
    public class BlueprintPlacementElement
    {
        public readonly Vector3Int Position;
        public readonly BlockDirection Direction;
        public readonly BlockId BlockId;
        public readonly Dictionary<string, string> Settings;

        public BlueprintPlacementElement(Vector3Int position, BlockDirection direction, BlockId blockId, Dictionary<string, string> settings)
        {
            Position = position;
            Direction = direction;
            BlockId = blockId;
            Settings = settings;
        }
    }
}
```

- [ ] **Step 2: 展開計算を実装**

`BlueprintPasteCalculator.cs`:
```csharp
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Blueprint
{
    public static class BlueprintPasteCalculator
    {
        public static List<BlueprintPlacementElement> CalculatePlacements(BlueprintJsonObject blueprint, Vector3Int pasteAnchor, int rotationStep)
        {
            var result = new List<BlueprintPlacementElement>();
            foreach (var block in blueprint.Blocks)
            {
                // マスタに無いGuid（mod構成変更）はスキップして継続する
                // Skip blocks whose GUID no longer exists in the master data
                var blockMaster = MasterHolder.BlockMaster.GetBlockMasterOrNull(block.BlockGuid);
                if (blockMaster == null) continue;

                var element = CalcElement(block, blockMaster.BlockSize);
                result.Add(element);
            }

            return result;

            #region Internal

            BlueprintPlacementElement CalcElement(BlueprintBlockJsonObject block, Vector3Int blockSize)
            {
                var direction = (BlockDirection)block.Direction;
                for (var i = 0; i < rotationStep; i++) direction = direction.HorizonRotation();

                // 原点と最大セルを回転し、成分ごとのminを新原点にする（マルチセル対応）
                // Rotate origin and max cell; take component-wise min as new origin
                var originalDirection = (BlockDirection)block.Direction;
                var maxOffset = BlockPositionInfo.CalcBlockMaxPos(block.Offset, originalDirection, blockSize);
                var rotatedOrigin = RotateOffset(block.Offset, rotationStep);
                var rotatedMax = RotateOffset(maxOffset, rotationStep);
                var newOrigin = Vector3Int.Min(rotatedOrigin, rotatedMax);

                var blockId = MasterHolder.BlockMaster.GetBlockId(block.BlockGuid);
                return new BlueprintPlacementElement(pasteAnchor + newOrigin, direction, blockId, block.Settings);
            }

            // 時計回り90度: (x, z) -> (z, -x)。HorizonRotation(North->East)と同回転
            // 90-degree clockwise: (x, z) -> (z, -x), matching HorizonRotation
            Vector3Int RotateOffset(Vector3Int offset, int steps)
            {
                var current = offset;
                for (var i = 0; i < steps; i++) current = new Vector3Int(current.z, current.y, -current.x);
                return current;
            }

            #endregion
        }
    }
}
```
注意: `MasterHolder.BlockMaster` に `GetBlockMasterOrNull(Guid)` が無い場合は、存在確認APIの実形（`TryGetBlockId` 等）を確認して置き換える。`HorizonRotation()` が North→East 以外の回転方向だった場合は `RotateOffset` を逆回転 `(x,z)->(-z,x)` に合わせる（Step 3 のテストで検出される）。

- [ ] **Step 3: 失敗するテストを書く**

`Tests/CombinedTest/Game/BlueprintPasteCalculatorTest.cs`:
```csharp
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class BlueprintPasteCalculatorTest
    {
        [Test]
        public void RotationTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid.ToString();
            var block = new BlueprintBlockJsonObject(new Vector3Int(2, 0, 3), chestGuid, (int)BlockDirection.North, new Dictionary<string, string>());
            var blueprint = new BlueprintJsonObject("rot", new List<BlueprintBlockJsonObject> { block });

            // 90度回転: (2,0,3) -> (3,0,-2)、North -> East
            // One clockwise step: offset (2,0,3) -> (3,0,-2), North -> East
            var rotated = BlueprintPasteCalculator.CalculatePlacements(blueprint, new Vector3Int(10, 0, 10), 1);
            Assert.AreEqual(new Vector3Int(13, 0, 8), rotated[0].Position);
            Assert.AreEqual(BlockDirection.North.HorizonRotation(), rotated[0].Direction);

            // 4回転で元に戻る（冪等性）
            // Four steps return to identity
            var full = BlueprintPasteCalculator.CalculatePlacements(blueprint, new Vector3Int(10, 0, 10), 4);
            Assert.AreEqual(new Vector3Int(12, 0, 13), full[0].Position);
            Assert.AreEqual(BlockDirection.North, full[0].Direction);
        }

        [Test]
        public void MultiCellRotationKeepsFootprintTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // マルチセルブロック（BlockSizeが(1,1,1)でないもの）で回転前後の占有セル集合を比較
            // Compare occupied-cell sets before/after rotation for a multi-cell block
            var blockId = ForUnitTestModBlockId.MachineId;
            var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var guid = master.BlockGuid.ToString();

            var block = new BlueprintBlockJsonObject(Vector3Int.zero, guid, (int)BlockDirection.North, new Dictionary<string, string>());
            var blueprint = new BlueprintJsonObject("multi", new List<BlueprintBlockJsonObject> { block });

            var placed = BlueprintPasteCalculator.CalculatePlacements(blueprint, Vector3Int.zero, 1)[0];

            // 回転後の原点からBlockPositionInfoを構築し、セル数がサイズ積と一致することを確認
            // Rebuild BlockPositionInfo at the rotated origin and verify cell count
            var info = new BlockPositionInfo(placed.Position, placed.Direction, master.BlockSize);
            var cellCount = 0;
            foreach (var _ in info.EnumeratePositions()) cellCount++;
            Assert.AreEqual(master.BlockSize.x * master.BlockSize.y * master.BlockSize.z, cellCount);

            // 回転前の全セルを直接回転した集合と、回転後BlockPositionInfoのセル集合が一致
            // Directly-rotated cells must equal the rotated BlockPositionInfo cells
            var originalInfo = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, master.BlockSize);
            var expected = new HashSet<Vector3Int>();
            foreach (var pos in originalInfo.EnumeratePositions()) expected.Add(new Vector3Int(pos.z, pos.y, -pos.x));
            var actual = new HashSet<Vector3Int>();
            foreach (var pos in info.EnumeratePositions()) actual.Add(pos);
            Assert.IsTrue(expected.SetEquals(actual));
        }

        [Test]
        public void UnknownGuidSkippedTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var block = new BlueprintBlockJsonObject(Vector3Int.zero, System.Guid.NewGuid().ToString(), 0, new Dictionary<string, string>());
            var blueprint = new BlueprintJsonObject("unknown", new List<BlueprintBlockJsonObject> { block });

            var result = BlueprintPasteCalculator.CalculatePlacements(blueprint, Vector3Int.zero, 0);
            Assert.AreEqual(0, result.Count);
        }
    }
}
```
`RotationTest` の期待値は `HorizonRotation()` が North→East（時計回り）である前提。テストが失敗した場合は `BlockDirectionExtension.HorizonRotation` の実装を読み、回転方向に合わせて `RotateOffset` と期待値を両方修正する（オフセット回転と向き回転が同一の回転であることが本質）。

- [ ] **Step 4: コンパイル＋テスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintPasteCalculatorTest"`
Expected: 全PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Blueprint moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintPasteCalculatorTest.cs
git commit -m "feat: ブループリント貼り付け展開計算（水平回転対応）を追加"
```

---

### Task 4: フィルタスプリッタの設定コピー（抽出＋注入）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/FilterSplitter/VanillaFilterSplitterComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaFilterSplitterTemplate.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintFilterSplitterSettingsTest.cs`

**Interfaces:**
- Consumes: `IBlockBlueprintSettings`（Task 2）、`BlueprintCreateService`（Task 2）
- Produces: `VanillaFilterSplitterComponent.BlueprintSettingsSaveKey`（const string）、`ApplyBlueprintSettingsJson(string json)`。BlockCreateParam の Key=`BlueprintSettingsSaveKey`、Value=設定JSONのUTF8バイト列という規約（クライアント貼り付け側 Task 7 が使用）

- [ ] **Step 1: コンポーネントに IBlockBlueprintSettings を実装**

`VanillaFilterSplitterComponent.cs` に追加（クラス宣言に `IBlockBlueprintSettings` を追加）:
```csharp
public const string BlueprintSettingsSaveKey = "FilterSplitterBlueprintSettings";
public string BlueprintSettingsKey => BlueprintSettingsSaveKey;

public string GetBlueprintSettingsJson()
{
    BlockException.CheckDestroy(this);

    // 方向ごとのモードとフィルタGUIDのみ（BufferedItemは実行時状態のため除外）
    // Mode and filter GUIDs per direction only; buffered items are runtime state
    var directions = new List<BlueprintDirectionSettingJsonObject>();
    foreach (var dir in _directions)
    {
        directions.Add(new BlueprintDirectionSettingJsonObject
        {
            ConnectorGuid = dir.ConnectorGuid.ToString(),
            Mode = (int)dir.Mode,
            FilterItemGuids = dir.FilterItems
                .Select(id => id == ItemMaster.EmptyItemId ? null : MasterHolder.ItemMaster.GetItemMaster(id).ItemGuid.ToString())
                .ToList(),
        });
    }

    return JsonConvert.SerializeObject(new BlueprintSettingsJsonObject { Directions = directions });
}

public void ApplyBlueprintSettingsJson(string json)
{
    var settings = JsonConvert.DeserializeObject<BlueprintSettingsJsonObject>(json);
    if (settings?.Directions == null) return;

    // ConnectorGuidで方向を突き合わせ、既存のSetMode/SetFilterItemで適用する
    // Match directions by ConnectorGuid and apply via existing setters
    foreach (var saved in settings.Directions)
    {
        var index = FindDirectionIndex(saved.ConnectorGuid);
        if (index < 0) continue;

        SetMode(index, (FilterSplitterMode)saved.Mode);
        for (var slot = 0; slot < saved.FilterItemGuids.Count && slot < _directions[index].FilterItems.Length; slot++)
        {
            var guidStr = saved.FilterItemGuids[slot];
            var itemId = guidStr == null ? ItemMaster.EmptyItemId : GetItemIdOrEmpty(guidStr);
            SetFilterItem(index, slot, itemId);
        }
    }

    #region Internal

    int FindDirectionIndex(string connectorGuidStr)
    {
        for (var i = 0; i < _directions.Length; i++)
        {
            if (_directions[i].ConnectorGuid.ToString() == connectorGuidStr) return i;
        }

        return -1;
    }

    ItemId GetItemIdOrEmpty(string guidStr)
    {
        var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(System.Guid.Parse(guidStr));
        return itemId ?? ItemMaster.EmptyItemId;
    }

    #endregion
}

public class BlueprintSettingsJsonObject
{
    [JsonProperty("directions")] public List<BlueprintDirectionSettingJsonObject> Directions;
}

public class BlueprintDirectionSettingJsonObject
{
    [JsonProperty("connectorGuid")] public string ConnectorGuid;
    [JsonProperty("mode")] public int Mode;
    [JsonProperty("filterItemGuids")] public List<string> FilterItemGuids;
}
```
注意: `DirectionState` の実フィールド名（`ConnectorGuid`/`FilterItems`/`Mode`）と `GetItemIdOrNull` の実シグネチャは既存コード（L306-427, L371-397）に合わせる。空アイテムのGUID表現は既存 `DirectionSaveJsonObject.FilterItemGuids` の慣例（null or 空文字）に一致させる。JsonObject 2クラスの追加でファイルが200行を超える場合は `FilterSplitterBlueprintSettingsJsonObject.cs` として同ディレクトリに分離する。

- [ ] **Step 2: テンプレートで createParams を消費**

`VanillaFilterSplitterTemplate.cs` — `New` が createParams から設定JSONを取り出し、コンポーネント生成後に適用する形へ変更:
```csharp
public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
{
    return GetBlock(null, ExtractBlueprintSettingsJson(createParams), blockMasterElement, blockInstanceId, blockPositionInfo);
}

public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
{
    return GetBlock(componentStates, null, blockMasterElement, blockInstanceId, blockPositionInfo);
}

// ブループリント設定はUTF8のJSON文字列としてBlockCreateParamに載る
// Blueprint settings arrive as a UTF8 JSON string inside BlockCreateParam
private static string ExtractBlueprintSettingsJson(BlockCreateParam[] createParams)
{
    foreach (var param in createParams)
    {
        if (param.Key != VanillaFilterSplitterComponent.BlueprintSettingsSaveKey) continue;
        return System.Text.Encoding.UTF8.GetString(param.Value);
    }

    return null;
}
```
`GetBlock` に `string blueprintSettingsJson` 引数を追加し、new側コンストラクタでコンポーネント生成後に:
```csharp
if (blueprintSettingsJson != null) splitterComponent.ApplyBlueprintSettingsJson(blueprintSettingsJson);
```

- [ ] **Step 3: 失敗するテストを書く**

`Tests/CombinedTest/Game/BlueprintFilterSplitterSettingsTest.cs`:
```csharp
using System;
using System.Text;
using Game.Block.Blocks.FilterSplitter;
using Game.Block.Interface;
using Game.Blueprint;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class BlueprintFilterSplitterSettingsTest
    {
        [Test]
        public void SettingsRoundTripThroughBlueprintTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 元のフィルタスプリッタに設定を入れる
            // Configure the source filter splitter
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FilterSplitterId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var source);
            var sourceComponent = source.GetComponent<VanillaFilterSplitterComponent>();
            sourceComponent.SetMode(0, FilterSplitterMode.Blacklist);
            sourceComponent.SetFilterItem(0, 0, ForUnitTestItemId.ItemId1);

            // 範囲抽出でBPを作り、設定JSONが入っていることを確認
            // Extract a blueprint and verify settings JSON is captured
            var created = BlueprintCreateService.TryCreateFromArea("filter", 0, 0, 0, 0, out var blueprint);
            Assert.IsTrue(created);
            var settingsJson = blueprint.Blocks[0].Settings[VanillaFilterSplitterComponent.BlueprintSettingsSaveKey];
            Assert.IsNotNull(settingsJson);

            // 設定JSONをBlockCreateParamに載せて別座標に設置し、設定が再現されることを確認
            // Place a new splitter with the settings param and verify reproduction
            var createParams = new[] { new BlockCreateParam(VanillaFilterSplitterComponent.BlueprintSettingsSaveKey, Encoding.UTF8.GetBytes(settingsJson)) };
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FilterSplitterId, new Vector3Int(10, 0, 10), BlockDirection.North, createParams, out var pasted);
            var pastedComponent = pasted.GetComponent<VanillaFilterSplitterComponent>();

            Assert.AreEqual(FilterSplitterMode.Blacklist, pastedComponent.GetMode(0));
            Assert.AreEqual(ForUnitTestItemId.ItemId1, pastedComponent.GetFilterItems(0)[0]);
        }
    }
}
```
注意: `ForUnitTestModBlockId.FilterSplitterId` が存在しない場合はテスト用マスタ（`Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`）へのフィルタスプリッタ定義追加とID定義追加が必要（既存のFilterSplitterTest系テストが使っているIDを流用する）。`GetMode`/`GetFilterItems` の実シグネチャは既存コンポーネント（L187-215）に合わせる。

- [ ] **Step 4: コンパイル＋テスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintFilterSplitterSettingsTest|FilterSplitter"`
Expected: 新テスト＋既存FilterSplitter系テスト全PASS（テンプレート変更のデグレ確認込み）

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Block moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintFilterSplitterSettingsTest.cs
git commit -m "feat: フィルタスプリッタ設定のブループリントコピー対応"
```

---

### Task 5: va:blueprint プロトコル（Create / GetAll / Delete）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`
- Modify: `Server.Protocol` のasmdef（references に `Game.Blueprint` 追加）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BlueprintProtocolTest.cs`

**Interfaces:**
- Consumes: `IBlueprintDatastore` / `BlueprintCreateService` / `BlueprintJsonObject`（Task 1-2）
- Produces: `BlueprintProtocol`（Tag `va:blueprint`）、`BlueprintRequest`（static factory: `CreateCreateRequest(string name, int minX, int minZ, int maxX, int maxZ)` / `CreateGetAllRequest()` / `CreateDeleteRequest(string name)`）、`BlueprintResponse`（`Success` / `FailureReason` / `List<BlueprintMessagePack> Blueprints` / `RegisteredName`）、`BlueprintMessagePack.ToJsonObject()`（クライアントがBP実データへ変換する口）

- [ ] **Step 1: プロトコルを作成**

`BlueprintProtocol.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Blueprint;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class BlueprintProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:blueprint";

        private readonly IBlueprintDatastore _blueprintDatastore;

        public BlueprintProtocol(ServiceProvider serviceProvider)
        {
            _blueprintDatastore = serviceProvider.GetService<IBlueprintDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<BlueprintRequest>(payload);

            switch (request.Operation)
            {
                case BlueprintOperation.Create:
                    return HandleCreate(request);
                case BlueprintOperation.GetAll:
                    return SuccessResponse(null);
                case BlueprintOperation.Delete:
                    return _blueprintDatastore.Delete(request.Name)
                        ? SuccessResponse(null)
                        : FailResponse(BlueprintFailureReason.NotFound);
                default:
                    return FailResponse(BlueprintFailureReason.UnknownOperation);
            }

            #region Internal

            ProtocolMessagePackBase HandleCreate(BlueprintRequest req)
            {
                if (string.IsNullOrEmpty(req.Name)) return FailResponse(BlueprintFailureReason.InvalidName);

                // 範囲抽出。対象ブロック0なら空BPを作らず失敗を返す
                // Extract from area; reject empty selections
                var created = BlueprintCreateService.TryCreateFromArea(req.Name, req.MinX, req.MinZ, req.MaxX, req.MaxZ, out var blueprint);
                if (!created) return FailResponse(BlueprintFailureReason.EmptyArea);

                var registeredName = _blueprintDatastore.Register(blueprint);
                return SuccessResponse(registeredName);
            }

            BlueprintResponse SuccessResponse(string registeredName)
            {
                var blueprints = _blueprintDatastore.Blueprints.Select(b => new BlueprintMessagePack(b)).ToList();
                return new BlueprintResponse(true, BlueprintFailureReason.None, registeredName, blueprints);
            }

            BlueprintResponse FailResponse(BlueprintFailureReason reason)
            {
                return new BlueprintResponse(false, reason, null, new List<BlueprintMessagePack>());
            }

            #endregion
        }
    }
}
```

- [ ] **Step 2: MessagePackクラスを同ファイルに追加**

`BlueprintProtocol.cs` の `#region MessagePack` として追加（クラス内）:
```csharp
#region MessagePack

[MessagePackObject]
public class BlueprintRequest : ProtocolMessagePackBase
{
    [Key(2)] public BlueprintOperation Operation { get; set; }
    [Key(3)] public string Name { get; set; }
    [Key(4)] public int MinX { get; set; }
    [Key(5)] public int MinZ { get; set; }
    [Key(6)] public int MaxX { get; set; }
    [Key(7)] public int MaxZ { get; set; }

    [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
    public BlueprintRequest() { Tag = ProtocolTag; }

    private BlueprintRequest(BlueprintOperation operation, string name, int minX, int minZ, int maxX, int maxZ)
    {
        Tag = ProtocolTag;
        Operation = operation;
        Name = name;
        MinX = minX;
        MinZ = minZ;
        MaxX = maxX;
        MaxZ = maxZ;
    }

    public static BlueprintRequest CreateCreateRequest(string name, int minX, int minZ, int maxX, int maxZ)
        => new(BlueprintOperation.Create, name, minX, minZ, maxX, maxZ);

    public static BlueprintRequest CreateGetAllRequest()
        => new(BlueprintOperation.GetAll, null, 0, 0, 0, 0);

    public static BlueprintRequest CreateDeleteRequest(string name)
        => new(BlueprintOperation.Delete, name, 0, 0, 0, 0);
}

[MessagePackObject]
public class BlueprintResponse : ProtocolMessagePackBase
{
    [Key(2)] public bool Success { get; set; }
    [Key(3)] public BlueprintFailureReason FailureReason { get; set; }
    [Key(4)] public string RegisteredName { get; set; }
    [Key(5)] public List<BlueprintMessagePack> Blueprints { get; set; }

    [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
    public BlueprintResponse() { }

    public BlueprintResponse(bool success, BlueprintFailureReason failureReason, string registeredName, List<BlueprintMessagePack> blueprints)
    {
        Tag = ProtocolTag;
        Success = success;
        FailureReason = failureReason;
        RegisteredName = registeredName;
        Blueprints = blueprints;
    }
}

[MessagePackObject]
public class BlueprintMessagePack
{
    [Key(0)] public string Name { get; set; }
    [Key(1)] public List<BlueprintBlockMessagePack> Blocks { get; set; }

    [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
    public BlueprintMessagePack() { }

    public BlueprintMessagePack(BlueprintJsonObject jsonObject)
    {
        Name = jsonObject.Name;
        Blocks = jsonObject.Blocks.Select(b => new BlueprintBlockMessagePack(b)).ToList();
    }

    // クライアントがBP実データ（貼り付け計算の入力）へ戻す口
    // Converts back to the domain model used by paste calculation
    public BlueprintJsonObject ToJsonObject()
    {
        return new BlueprintJsonObject(Name, Blocks.Select(b => b.ToJsonObject()).ToList());
    }
}

[MessagePackObject]
public class BlueprintBlockMessagePack
{
    [Key(0)] public Vector3IntMessagePack Offset { get; set; }
    [Key(1)] public string BlockGuidStr { get; set; }
    [Key(2)] public int Direction { get; set; }
    [Key(3)] public Dictionary<string, string> Settings { get; set; }

    [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
    public BlueprintBlockMessagePack() { }

    public BlueprintBlockMessagePack(BlueprintBlockJsonObject jsonObject)
    {
        Offset = new Vector3IntMessagePack(jsonObject.Offset);
        BlockGuidStr = jsonObject.BlockGuidStr;
        Direction = jsonObject.Direction;
        Settings = jsonObject.Settings;
    }

    public BlueprintBlockJsonObject ToJsonObject()
    {
        return new BlueprintBlockJsonObject(Offset.Vector3Int, BlockGuidStr, Direction, Settings);
    }
}

public enum BlueprintOperation
{
    Create = 0,
    GetAll = 1,
    Delete = 2,
}

public enum BlueprintFailureReason
{
    None = 0,
    InvalidName = 1,
    EmptyArea = 2,
    NotFound = 3,
    UnknownOperation = 4,
}

#endregion
```
ファイルが200行を超える場合は MessagePack 群を `BlueprintPacketDto.cs`（同ディレクトリ、`PlacePacketDto.cs` と同じ流儀）へ分離する。

- [ ] **Step 3: PacketResponseCreator へ登録**

`PacketResponseCreator.cs` コンストラクタ末尾（既存最終Add行の直後）に追加:
```csharp
_packetResponseDictionary.Add(BlueprintProtocol.ProtocolTag, new BlueprintProtocol(serviceProvider));
```
`Server.Protocol` のasmdefに `Game.Blueprint` 参照を追加。

- [ ] **Step 4: 失敗するテストを書く**

`Tests/CombinedTest/Server/PacketTest/BlueprintProtocolTest.cs`:
```csharp
using System;
using Game.Block.Interface;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BlueprintProtocolTest
    {
        [Test]
        public void CreateGetAllDeleteFlowTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // Create: 範囲内ブロックからBPが登録される
            // Create registers a blueprint from the area
            var createResponse = Send(BlueprintProtocol.BlueprintRequest.CreateCreateRequest("base", 0, 0, 5, 5));
            Assert.IsTrue(createResponse.Success);
            Assert.AreEqual("base", createResponse.RegisteredName);
            Assert.AreEqual(1, createResponse.Blueprints.Count);
            Assert.AreEqual(1, createResponse.Blueprints[0].Blocks.Count);

            // GetAll: 登録済みBPが返る
            // GetAll returns registered blueprints
            var getAllResponse = Send(BlueprintProtocol.BlueprintRequest.CreateGetAllRequest());
            Assert.IsTrue(getAllResponse.Success);
            Assert.AreEqual(1, getAllResponse.Blueprints.Count);
            Assert.AreEqual("base", getAllResponse.Blueprints[0].Name);

            // Delete: 削除後は0件
            // Delete removes the blueprint
            var deleteResponse = Send(BlueprintProtocol.BlueprintRequest.CreateDeleteRequest("base"));
            Assert.IsTrue(deleteResponse.Success);
            Assert.AreEqual(0, deleteResponse.Blueprints.Count);

            #region Internal

            BlueprintProtocol.BlueprintResponse Send(BlueprintProtocol.BlueprintRequest request)
            {
                var payload = MessagePackSerializer.Serialize(request);
                var responses = packet.GetPacketResponse(payload);
                return MessagePackSerializer.Deserialize<BlueprintProtocol.BlueprintResponse>(responses[0]);
            }

            #endregion
        }

        [Test]
        public void CreateFailuresTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 空範囲はEmptyArea、空文字名はInvalidName、存在しない削除はNotFound
            // Empty area, empty name, and missing-delete failures
            var empty = Send(BlueprintProtocol.BlueprintRequest.CreateCreateRequest("x", 50, 50, 55, 55));
            Assert.IsFalse(empty.Success);
            Assert.AreEqual(BlueprintProtocol.BlueprintFailureReason.EmptyArea, empty.FailureReason);

            var noName = Send(BlueprintProtocol.BlueprintRequest.CreateCreateRequest("", 0, 0, 5, 5));
            Assert.IsFalse(noName.Success);
            Assert.AreEqual(BlueprintProtocol.BlueprintFailureReason.InvalidName, noName.FailureReason);

            var missingDelete = Send(BlueprintProtocol.BlueprintRequest.CreateDeleteRequest("missing"));
            Assert.IsFalse(missingDelete.Success);
            Assert.AreEqual(BlueprintProtocol.BlueprintFailureReason.NotFound, missingDelete.FailureReason);

            #region Internal

            BlueprintProtocol.BlueprintResponse Send(BlueprintProtocol.BlueprintRequest request)
            {
                var payload = MessagePackSerializer.Serialize(request);
                var responses = packet.GetPacketResponse(payload);
                return MessagePackSerializer.Deserialize<BlueprintProtocol.BlueprintResponse>(responses[0]);
            }

            #endregion
        }
    }
}
```
注意: enum・MessagePackクラスのネスト位置（プロトコルクラス内 or 名前空間直下）は実装に合わせてテストの参照を調整。`packet.GetPacketResponse` のシグネチャ（context引数の有無）は既存 `PlaceBlockProtocolTest` に合わせる。

- [ ] **Step 5: コンパイル＋テスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintProtocolTest"`
Expected: 全PASS

- [ ] **Step 6: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BlueprintProtocolTest.cs
git commit -m "feat: va:blueprintプロトコル追加（Create/GetAll/Delete、Operation分岐）"
```

---

### Task 6: クライアントAPI＋BPライブラリキャッシュ

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/ClientBlueprintLibrary.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（DI登録）

**Interfaces:**
- Consumes: `BlueprintProtocol.BlueprintRequest/BlueprintResponse`（Task 5）
- Produces: `VanillaApiWithResponse.SendBlueprintRequest(BlueprintProtocol.BlueprintRequest request, CancellationToken ct)`、`ClientBlueprintLibrary`（`IReadOnlyList<BlueprintMessagePack> Blueprints` / `async UniTask Refresh(CancellationToken ct)` / `async UniTask<(bool success, string registeredName)> CreateBlueprint(string name, int minX, int minZ, int maxX, int maxZ, CancellationToken ct)` / `async UniTask DeleteBlueprint(string name, CancellationToken ct)`）

- [ ] **Step 1: VanillaApiメソッドを追加（1プロトコル=1メソッド）**

`VanillaApiWithResponse.cs` に追加:
```csharp
public async UniTask<BlueprintProtocol.BlueprintResponse> SendBlueprintRequest(BlueprintProtocol.BlueprintRequest request, CancellationToken ct)
{
    return await _packetExchangeManager.GetPacketResponse<BlueprintProtocol.BlueprintResponse>(request, ct);
}
```

- [ ] **Step 2: クライアント側ライブラリキャッシュを作成**

`ClientBlueprintLibrary.cs`:
```csharp
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    /// <summary>
    ///     サーバーのBPライブラリのクライアント側キャッシュ
    ///     Client-side cache of the server blueprint library
    /// </summary>
    public class ClientBlueprintLibrary
    {
        private readonly List<BlueprintProtocol.BlueprintMessagePack> _blueprints = new();

        public IReadOnlyList<BlueprintProtocol.BlueprintMessagePack> Blueprints => _blueprints;

        public async UniTask Refresh(CancellationToken ct)
        {
            var response = await ClientContext.VanillaApi.Response.SendBlueprintRequest(BlueprintProtocol.BlueprintRequest.CreateGetAllRequest(), ct);
            ApplyResponse(response);
        }

        public async UniTask<(bool success, string registeredName)> CreateBlueprint(string name, int minX, int minZ, int maxX, int maxZ, CancellationToken ct)
        {
            var request = BlueprintProtocol.BlueprintRequest.CreateCreateRequest(name, minX, minZ, maxX, maxZ);
            var response = await ClientContext.VanillaApi.Response.SendBlueprintRequest(request, ct);
            ApplyResponse(response);
            return (response.Success, response.RegisteredName);
        }

        public async UniTask DeleteBlueprint(string name, CancellationToken ct)
        {
            var response = await ClientContext.VanillaApi.Response.SendBlueprintRequest(BlueprintProtocol.BlueprintRequest.CreateDeleteRequest(name), ct);
            ApplyResponse(response);
        }

        private void ApplyResponse(BlueprintProtocol.BlueprintResponse response)
        {
            // 全レスポンスが最新の全件を返すため、常に置き換えるだけでよい
            // Every response carries the full list, so replace unconditionally
            if (!response.Success && response.FailureReason != BlueprintProtocol.BlueprintFailureReason.None) return;

            _blueprints.Clear();
            _blueprints.AddRange(response.Blueprints);
        }
    }
}
```
注意: 失敗レスポンス（EmptyArea等）はBlueprintsが空で返るため、`Success == false` の場合はキャッシュを置き換えない実装とした。`ClientContext.VanillaApi` のアクセス形は既存クライアントコード（`PlaceSystemUtil.SendPlaceBlockProtocol`）に合わせる。

- [ ] **Step 3: DI登録**

`MainGameStarter.cs` のPlaceSystem群登録付近（L182-192）に追加:
```csharp
builder.Register<ClientBlueprintLibrary>(Lifetime.Singleton);
```

- [ ] **Step 4: コンパイル＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

```bash
git add moorestech_client/Assets/Scripts/Client.Network moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint moorestech_client/Assets/Scripts/Client.Starter
git commit -m "feat: ブループリントのクライアントAPI・ライブラリキャッシュ追加"
```

---

### Task 7: 貼り付けモード（BlueprintPasteSystem＋プレビュー＋ビルドメニュー統合）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlacementSelection.cs`（enum値＋setter追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemSelector.cs`（分岐追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/IPlaceSystem.cs`（`PlaceSystemUpdateContext` にBP名を追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs`（`CreateContext()` のBP名生成・前フレーム比較追加）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/BlueprintPasteSystem.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/BlueprintPastePreviewController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuEntry.cs`（BP名フィールド＋BP用コンストラクタ）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuEntryCatalog.cs`（BPエントリ生成）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuView.cs`（開いた時にライブラリRefresh・nullアイコン耐性）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs`（BP選択の分岐）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（BlueprintPasteSystem登録）
- Modify: `Client.Game` のasmdef（`Game.Blueprint` 参照追加）

**Interfaces:**
- Consumes: `ClientBlueprintLibrary`（Task 6）、`BlueprintPasteCalculator` / `BlueprintPlacementElement`（Task 3）、`BlueprintMessagePack.ToJsonObject()`（Task 5）、`VanillaFilterSplitterComponent.BlueprintSettingsSaveKey` 規約（Task 4: 設定はUTF8 JSONバイトでCreateParamsへ）
- Produces: `PlacementSelectionType.Blueprint`、`PlacementSelection.SetSelectedBlueprint(string blueprintName)`、`PlaceSystemUpdateContext.SelectedBlueprintName`

- [ ] **Step 1: 選択種別とコンテキストを拡張**

`PlacementSelection.cs`:
```csharp
// enumに追加 / Add to enum
Blueprint,

// PlacementSelectionクラスに追加 / Add to PlacementSelection
public string SelectedBlueprintName { get; private set; }

public void SetSelectedBlueprint(string blueprintName)
{
    ClearSelection();
    SelectionType = PlacementSelectionType.Blueprint;
    SelectedBlueprintName = blueprintName;
}
```
`ClearSelection()` に `SelectedBlueprintName = null;` を追加。`PlaceSystemUpdateContext`（IPlaceSystem.cs）に `SelectedBlueprintName` フィールドを追加し、`PlaceSystemStateController.CreateContext()` の生成・変更検知（前フレーム比較）にも同フィールドを追加する。

- [ ] **Step 2: 貼り付けプレビューコントローラを作成**

`BlueprintPastePreviewController.cs` — `BlockPlacePreviewObjectPool` を直接使い、複数ブロック種のゴーストを同時表示する:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Game.Blueprint;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    /// <summary>
    ///     BP貼り付けのゴースト一括表示。複数ブロック種を同時にプールから取得する
    ///     Batch ghost display for paste; pulls multiple block kinds from the pool
    /// </summary>
    public class BlueprintPastePreviewController
    {
        private readonly BlockPlacePreviewObjectPool _pool;

        public BlueprintPastePreviewController(Transform parentTransform)
        {
            _pool = new BlockPlacePreviewObjectPool(parentTransform);
        }

        public void UpdatePreview(List<BlueprintPlacementElement> placements, List<bool> placeableFlags)
        {
            _pool.AllUnUse();
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                var previewObject = _pool.GetObject(placement.BlockId);
                previewObject.SetTransform(placement.Position, placement.Direction.GetRotation());
                previewObject.SetPlaceableColor(placeableFlags[i]);
                previewObject.SetActive(true);
            }
        }

        public void Hide()
        {
            _pool.AllUnUse();
        }
    }
}
```
注意: `SetTransform` の引数（ワールド座標変換・`GetBlockModelOriginPos` 等のオフセット）は `PlacementPreviewBlockGameObjectController.SetPreviewAndGroundDetect`（L39-54）の既存実装の座標計算に合わせる。親Transformは既存 `PlacementPreviewBlockGameObjectController` のtransformを流用するか、`BlueprintPasteSystem` 生成時に `new GameObject("BlueprintPastePreview").transform` を作る。

- [ ] **Step 3: BlueprintPasteSystem を作成**

`BlueprintPasteSystem.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Input;
using Game.Block.Interface;
using Game.Blueprint;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    public class BlueprintPasteSystem : IPlaceSystem
    {
        private readonly ClientBlueprintLibrary _library;
        private readonly Camera _mainCamera;
        private BlueprintPastePreviewController _previewController;

        private BlueprintJsonObject _currentBlueprint;
        private int _rotationStep;

        public BlueprintPasteSystem(ClientBlueprintLibrary library)
        {
            _library = library;
            _mainCamera = Camera.main;
        }

        public void Enable()
        {
            _rotationStep = 0;
            _previewController ??= new BlueprintPastePreviewController(new GameObject("BlueprintPastePreview").transform);
        }

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            // 選択変更時にBP実データを解決する
            // Resolve blueprint data when the selection changes
            if (context.IsSelectionChanged) ResolveBlueprint(context.SelectedBlueprintName);
            if (_currentBlueprint == null) return;

            // Rキーで90度回転
            // Rotate 90 degrees with the rotation key
            if (InputManager.Playable.BlockPlaceRotation.GetKeyDown) _rotationStep = (_rotationStep + 1) % 4;

            if (!PlaceSystemUtil.TryGetRayHitPosition(_mainCamera, out var hitPoint)) return;
            var anchor = Vector3Int.RoundToInt(hitPoint);

            var placements = BlueprintPasteCalculator.CalculatePlacements(_currentBlueprint, anchor, _rotationStep);
            var placeableFlags = placements.Select(IsPlaceable).ToList();
            _previewController.UpdatePreview(placements, placeableFlags);

            // 左クリックで設置可能セルのみ送信（既存の部分設置挙動）
            // Left click sends placeable cells; server allows partial success
            if (InputManager.Playable.ScreenLeftClick.GetKeyUp) SendPlace(placements, placeableFlags);

            #region Internal

            void SendPlace(List<BlueprintPlacementElement> allPlacements, List<bool> flags)
            {
                var placeInfos = new List<PlaceInfo>();
                for (var i = 0; i < allPlacements.Count; i++)
                {
                    if (!flags[i]) continue;
                    placeInfos.Add(ToPlaceInfo(allPlacements[i]));
                }

                if (placeInfos.Count == 0) return;
                PlaceSystemUtil.SendPlaceBlockProtocol(placeInfos);
            }

            #endregion
        }

        public void Disable()
        {
            _previewController?.Hide();
            _currentBlueprint = null;
        }

        private void ResolveBlueprint(string blueprintName)
        {
            var pack = _library.Blueprints.FirstOrDefault(b => b.Name == blueprintName);
            _currentBlueprint = pack?.ToJsonObject();
        }

        private bool IsPlaceable(BlueprintPlacementElement placement)
        {
            // 既存ブロックとの重なりチェック（サーバー側でも再検証される）
            // Overlap check against existing blocks; server re-validates
            return !ServerContext.WorldBlockDatastore.Exists(placement.Position);
        }

        private static PlaceInfo ToPlaceInfo(BlueprintPlacementElement placement)
        {
            // 設定JSONをUTF8バイト化してCreateParamsへ載せる（Task 4の規約）
            // Encode settings JSON as UTF8 bytes into CreateParams
            var createParams = placement.Settings
                .Select(kvp => new BlockCreateParam(kvp.Key, Encoding.UTF8.GetBytes(kvp.Value)))
                .ToArray();

            return new PlaceInfo
            {
                Position = placement.Position,
                Direction = placement.Direction,
                VerticalDirection = BlockVerticalDirection.Horizontal,
                BlockId = placement.BlockId,
                Placeable = true,
                CreateParams = createParams,
            };
        }
    }
}
```
注意: `PlaceSystemUtil.TryGetRayHitPosition` の実シグネチャ（L31-51）とグリッドスナップ（`CalcPlacePoint` L86-136）は既存実装を確認して合わせる。クライアント側の重なりチェックは既存クライアントの流儀（`CommonBlockPlacePointCalculator.CalcPlaceable` L111-127 が使う `_blockGameObjectDataStore.IsOverlapPositionInfo`）に置き換えてもよい（`ServerContext.WorldBlockDatastore` がクライアントで参照できない場合はそちらを使う）。マルチセルブロックの重なりは原点セルのみのチェックだと不十分のため、`BlockPositionInfo` を構築して `EnumeratePositions()` 全セルをチェックする。

- [ ] **Step 4: PlaceSystemSelector と DI に配線**

`PlaceSystemSelector.cs` — コンストラクタに `BlueprintPasteSystem` を追加し、switchに分岐追加:
```csharp
case PlacementSelectionType.Blueprint:
    return _blueprintPasteSystem;
```
`MainGameStarter.cs` L182-192付近に `builder.Register<BlueprintPasteSystem>(Lifetime.Singleton);` を追加。

- [ ] **Step 5: ビルドメニューにBPエントリを追加**

`BuildMenuEntry.cs` — `string BlueprintName` フィールドとBP用コンストラクタを追加（`EntryType = PlacementSelectionType.Blueprint`、`IconView = null`、`ToolTipText = blueprintName`）。

`BuildMenuEntryCatalog.cs` — `CreateEntries` に `ClientBlueprintLibrary` 由来のBPリストを引数追加し、接続ツールの後にBPエントリ群を生成:
```csharp
// 保存済みブループリントのエントリを追加する
// Append entries for saved blueprints
foreach (var blueprint in blueprintLibrary.Blueprints)
{
    entries.Add(new BuildMenuEntry(blueprint.Name));
}
```
呼び出し元 `BuildMenuView.RebuildEntryList()` に `ClientBlueprintLibrary` を渡す（ViewへのDIは `MainGameStarter` のSerializeField/RegisterComponent既存経路を確認して合わせる）。

`BuildMenuView.cs` — `SetActive(true)` 時に `_blueprintLibrary.Refresh(cts.Token).Forget();` を呼び、完了後に `RebuildEntryList()` を再実行。スロット表示処理でアイコンがnullの場合はスプライト設定をスキップしテキストのみ表示する。

`BuildMenuState.cs` — `TryConsumeSelectedEntry` 後のswitchに追加:
```csharp
case PlacementSelectionType.Blueprint:
    _placementSelection.SetSelectedBlueprint(entry.BlueprintName);
    return new UITransitContext(UIStateEnum.PlaceBlock);
```

- [ ] **Step 6: コンパイル＋既存テストのデグレ確認＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceBlock"`
Expected: エラー0・既存設置テスト全PASS

```bash
git add moorestech_client/Assets/Scripts/Client.Game moorestech_client/Assets/Scripts/Client.Starter
git commit -m "feat: ブループリント貼り付けモード（プレビュー・回転・ビルドメニュー統合）"
```

---

### Task 8: コピーモード（矩形選択＋名前入力＋Create送信）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/BlueprintCopySystem.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/BlueprintAreaVisualizer.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Blueprint/BlueprintNameInputView.cs`
- Modify: `PlacementSelection.cs` / `PlaceSystemSelector.cs` / `BuildMenuEntry.cs` / `BuildMenuEntryCatalog.cs` / `BuildMenuState.cs`（コピーツールエントリ＝`ConnectTool` と同様の「ツール」導線）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`

**Interfaces:**
- Consumes: `ClientBlueprintLibrary.CreateBlueprint(...)`（Task 6）
- Produces: `PlacementSelectionType.BlueprintCopy`、`PlacementSelection.SetSelectedBlueprintCopyTool()`

- [ ] **Step 1: 選択種別にコピーツールを追加**

`PlacementSelection.cs` に `BlueprintCopy` enum値と `SetSelectedBlueprintCopyTool()`（`ClearSelection()` → `SelectionType = PlacementSelectionType.BlueprintCopy`）を追加。`BuildMenuEntryCatalog` の接続ツール群の並びに「ブループリントコピー」エントリを1件追加し、`BuildMenuState` のswitchに `SetSelectedBlueprintCopyTool()` → PlaceBlock遷移を追加。

- [ ] **Step 2: 範囲可視化を作成**

`BlueprintAreaVisualizer.cs` — プリミティブQuadを実行時生成して矩形を表示（Prefab不要・半透明マテリアル）:
```csharp
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    /// <summary>
    ///     ドラッグ中の選択矩形を地面上に半透明表示する
    ///     Shows the drag-selection rect as a translucent quad on the ground
    /// </summary>
    public class BlueprintAreaVisualizer
    {
        private readonly GameObject _quad;

        public BlueprintAreaVisualizer()
        {
            _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quad.name = "BlueprintAreaVisualizer";
            Object.Destroy(_quad.GetComponent<Collider>());
            _quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // 既存の半透明シェーダ設定に合わせる（BlockPreviewObjectのマテリアル設定を参照）
            // Match the existing translucent material setup used by BlockPreviewObject
            var renderer = _quad.GetComponent<MeshRenderer>();
            renderer.material.color = new Color(0.2f, 0.6f, 1f, 0.35f);
            _quad.SetActive(false);
        }

        public void Show(int minX, int minZ, int maxX, int maxZ, float y)
        {
            var sizeX = maxX - minX + 1;
            var sizeZ = maxZ - minZ + 1;
            _quad.transform.position = new Vector3(minX + sizeX * 0.5f, y + 0.05f, minZ + sizeZ * 0.5f);
            _quad.transform.localScale = new Vector3(sizeX, sizeZ, 1f);
            _quad.SetActive(true);
        }

        public void Hide()
        {
            _quad.SetActive(false);
        }
    }
}
```

- [ ] **Step 3: 名前入力UIスクリプトを作成**

`BlueprintNameInputView.cs`（MonoBehaviour。UIオブジェクト本体はStep 5でuloop経由生成）:
```csharp
using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Blueprint
{
    public class BlueprintNameInputView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private readonly Subject<string> _onConfirm = new();
        private readonly Subject<Unit> _onCancel = new();

        public IObservable<string> OnConfirm => _onConfirm;
        public IObservable<Unit> OnCancel => _onCancel;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            confirmButton.onClick.AddListener(() =>
            {
                if (string.IsNullOrWhiteSpace(nameInputField.text)) return;
                _onConfirm.OnNext(nameInputField.text.Trim());
                Close();
            });
            cancelButton.onClick.AddListener(() =>
            {
                _onCancel.OnNext(Unit.Default);
                Close();
            });
        }

        public void Open()
        {
            nameInputField.text = "";
            gameObject.SetActive(true);
            nameInputField.ActivateInputField();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
```

- [ ] **Step 4: BlueprintCopySystem を作成**

`BlueprintCopySystem.cs` — `DeleteObjectService`（ドラッグ選択・ESC2段階キャンセル）を雛形にした矩形ドラッグ:
```csharp
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Blueprint;
using Client.Input;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    public class BlueprintCopySystem : IPlaceSystem
    {
        private readonly ClientBlueprintLibrary _library;
        private readonly BlueprintNameInputView _nameInputView;
        private readonly Camera _mainCamera;
        private BlueprintAreaVisualizer _visualizer;

        private bool _isDragging;
        private Vector3Int _dragStart;
        private Vector3Int _dragEnd;
        private bool _isAwaitingName;

        public BlueprintCopySystem(ClientBlueprintLibrary library, BlueprintNameInputView nameInputView)
        {
            _library = library;
            _nameInputView = nameInputView;
            _mainCamera = Camera.main;
        }

        public void Enable()
        {
            _visualizer ??= new BlueprintAreaVisualizer();
            SubscribeNameInput();
        }

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            // 名前入力ダイアログ表示中はドラッグ操作を止める
            // Freeze drag interaction while the name dialog is open
            if (_isAwaitingName) return;

            HandleDragStart();
            UpdateDrag();
            HandleRelease();
            HandleCancel();

            #region Internal

            void HandleDragStart()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyDown) return;
                if (EventSystem.current.IsPointerOverGameObject()) return;
                if (!PlaceSystemUtil.TryGetRayHitPosition(_mainCamera, out var hit)) return;

                _dragStart = Vector3Int.RoundToInt(hit);
                _dragEnd = _dragStart;
                _isDragging = true;
            }

            void UpdateDrag()
            {
                if (!_isDragging || !InputManager.Playable.ScreenLeftClick.GetKey) return;
                if (!PlaceSystemUtil.TryGetRayHitPosition(_mainCamera, out var hit)) return;

                _dragEnd = Vector3Int.RoundToInt(hit);
                var (minX, minZ, maxX, maxZ) = CalcRect();
                _visualizer.Show(minX, minZ, maxX, maxZ, _dragStart.y);
            }

            void HandleRelease()
            {
                if (!_isDragging || !InputManager.Playable.ScreenLeftClick.GetKeyUp) return;

                _isDragging = false;
                _isAwaitingName = true;
                _nameInputView.Open();
            }

            void HandleCancel()
            {
                if (!InputManager.UI.CloseUI.GetKeyDown) return;
                ResetSelection();
            }

            #endregion
        }

        public void Disable()
        {
            ResetSelection();
            _nameInputView.Close();
            _isAwaitingName = false;
        }

        private (int minX, int minZ, int maxX, int maxZ) CalcRect()
        {
            return (Mathf.Min(_dragStart.x, _dragEnd.x), Mathf.Min(_dragStart.z, _dragEnd.z),
                Mathf.Max(_dragStart.x, _dragEnd.x), Mathf.Max(_dragStart.z, _dragEnd.z));
        }

        private void ResetSelection()
        {
            _isDragging = false;
            _visualizer?.Hide();
        }

        private void SubscribeNameInput()
        {
            // 確定でCreate送信、キャンセルで選択解除（購読は初回のみ）
            // Confirm sends Create; cancel clears the selection
            _nameInputView.OnConfirm.Subscribe(name =>
            {
                var (minX, minZ, maxX, maxZ) = CalcRect();
                _library.CreateBlueprint(name, minX, minZ, maxX, maxZ, CancellationToken.None).Forget();
                _isAwaitingName = false;
                ResetSelection();
            }).AddTo(_nameInputView);

            _nameInputView.OnCancel.Subscribe(_ =>
            {
                _isAwaitingName = false;
                ResetSelection();
            }).AddTo(_nameInputView);
        }
    }
}
```
注意: `SubscribeNameInput` はEnable毎に重複購読しないよう、コンストラクタ移行かフラグ管理にする（`AddTo(_nameInputView)` はView破棄時解放のため、Enable複数回で購読が増える。実装時はコンストラクタで1回だけ購読する形が正）。Create失敗（EmptyArea）時のフィードバックはv1では無し（ライブラリが増えないことで気づける）。

- [ ] **Step 5: 名前入力UIのシーンオブジェクトをuloopで生成・配線**

Prefab/Scene直接編集は禁止のため、`uloop execute-dynamic-code` でメインゲームシーンのUIキャンバス配下に生成する:
1. `uloop-get-hierarchy` でインゲームUIのCanvasパスを確認
2. `uloop execute-dynamic-code` で `BlueprintNameInputView` 用のGameObject（パネル＋`TMP_InputField`＋確定/キャンセルButton＋TextMeshProUGUIラベル）を生成し、`[SerializeField]` 3参照をSerializedObject経由で配線、初期非アクティブに設定、シーン保存
3. `MainGameStarter.cs` に `[SerializeField] private BlueprintNameInputView blueprintNameInputView;` を追加し `RegisterComponent` 系の既存経路（L292付近）で登録、SerializeField参照も `uloop execute-dynamic-code` で配線

`PlaceSystemSelector` に `BlueprintCopy` 分岐、`MainGameStarter` に `builder.Register<BlueprintCopySystem>(Lifetime.Singleton);` を追加。

- [ ] **Step 6: コンパイル＋コミット**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

```bash
git add moorestech_client/Assets/Scripts moorestech_client/Assets/*.unity 2>/dev/null || git add moorestech_client/Assets/Scripts
git commit -m "feat: ブループリントコピーモード（矩形選択・名前入力・登録）"
```
（シーン変更ファイルはUnityが保存したもののみコミット）

---

### Task 9: 統合検証（回帰テスト＋実プレイ確認）

**Files:** なし（検証のみ。修正が出た場合は該当タスクのファイルへ）

- [ ] **Step 1: サーバー側全回帰**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests.CombinedTest|Tests.UnitTest"`
Expected: 全PASS（ブループリント関連の新規テスト含む）

- [ ] **Step 2: 実プレイ検証（プレイテストDSL）**

`unity-playmode-recorded-playtest` スキルを起動し、以下のシナリオを実行:
1. ブロックを2〜3個設置（チェスト＋機械）
2. ビルドメニュー→ブループリントコピー→矩形ドラッグ→名前入力→登録
3. ビルドメニュー再表示→登録したBPエントリを選択→R回転→別の場所に貼り付け
4. 貼り付け結果（ブロック種・向き・位置関係）をスクリーンショットとサーバー状態ダンプで確認

Expected: コピー元と同じ配置関係（回転考慮）で設置され、エラーログが無い

- [ ] **Step 3: セーブ・ロード実機確認**

プレイテスト中にBP登録→セーブ→再ロードし、ビルドメニューにBPが残っていることを確認。

- [ ] **Step 4: 最終コミット**

```bash
git add -A
git commit -m "test: ブループリント機能の統合検証と修正"
```

---

## 実装順の依存関係

```
Task 1 (Game.Blueprint基盤) → Task 2 (抽出) → Task 4 (FilterSplitter設定)
                             → Task 3 (展開計算)
Task 2,4 → Task 5 (プロトコル) → Task 6 (クライアントAPI) → Task 7 (貼り付け) → Task 9
Task 3 ────────────────────────────────────────────────────↗
Task 6 → Task 8 (コピー) → Task 9
```

## 既知の要調整ポイント（実装者向け）

- `HorizonRotation()` の回転方向（North→East想定）が逆の場合、`BlueprintPasteCalculator.RotateOffset` と Task 3 テスト期待値を対で修正
- `ComponentManager.GetComponents<T>` / `GetBlockMasterOrNull` / `GetItemIdOrNull` 等のAPI実名は各ファイルの既存実装を必ず確認
- `ForUnitTestModBlockId.FilterSplitterId` の実名（既存FilterSplitterテストで使用中のIDを流用）
- asmdef参照がGUID形式の場合、テキスト追記ではなくUnity Editor経由（uloop execute-dynamic-code）で追加
- BuildMenuViewのアイコンnull耐性はスロットPrefabの実装次第で対応箇所が変わる
