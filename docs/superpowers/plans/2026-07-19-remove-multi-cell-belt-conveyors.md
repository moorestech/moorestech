# Remove Multi-Cell Belt Conveyors Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** n連ベルトコンベアをマスター、コード、テスト、移行ツール、Prefabから完全に削除し、ベルトファミリーを1セル直線＋任意の上り/下りへ単純化する。

**Architecture:** `beltConveyorFamilies`を単一の`straightBlockGuid`契約へ変更し、`Game.Block.Interface`の解決済みモデルも単一の`StraightBlockId`を持つ。既存のクライアント配置パイプラインは維持し、長尺ラン分解だけを各セルの直線/上り/下りブロックへの1対1変換に置換する。

**Tech Stack:** Unity 6、C#、NUnit、YAML Source Generator、JSON、Unity AssetDatabase、uloop

## Global Constraints

- `tree2`ワークツリー `/Users/katsumi/moorestech-worktrees/tree2` で実装する。
- 後方互換、パフォーマンス最適化、将来拡張性は考慮しない。
- `Mooresmaster.Model.*`生成物を手動編集しない。
- Unity固有YAMLとPrefabをテキスト編集せず、Prefab削除はUnity Editor経由で行う。
- `.meta`を手動作成しない。
- C#変更後は必ずUnityコンパイルを行う。
- 1ファイル200行以下、`partial`禁止、`try-catch`原則禁止。
- 作業終了前にmainリポジトリと`moorestech_master`の全変更をコミットする。

---

## 配置と前例レビュー

| 項目 | 配置先 | 機構・前例 | 判定 |
|---|---|---|---|
| `BeltConveyorFamily`単純化 | `Game.Block.Interface/Extension` | 既存ファミリードメインモデルを同じ層で置換 | 適合 |
| ファミリー解決 | `Game.Block.Interface/Extension/BeltConveyorPlaceFamilyUtil` | マスタ生成物を読むstatic utilという既存配置 | 適合 |
| ファミリー検証 | `Core.Master/Validator` | `BlockMasterUtil`から呼ぶ既存ロード時検証 | 適合 |
| セルのブロック割当 | `Client.Game/.../BeltConveyor/Parts` | 既存`BeltConveyorRunDecomposer`と同じ配置パイプライン内の純変換 | 適合 |
| Prefab削除 | Unity `AssetDatabase.DeleteAsset` | Unity固有ファイルをEditor管理下で変更する規約 | 適合 |

データフローは `ドラッグ入力 → BeltConveyorPlacePointCalculator → セル列 → BeltConveyorCellBlockResolver → Preview/PlaceBlockProtocol` のまま維持する。新規コンポーネントは既存の長尺分解器を置換する「同じ位置の変換器」であり、書き込み経路、イベント、通信、状態は増やさない。

### 操作死活表

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 単連ベルトのクリック設置 | 生存 | 水平セルを`StraightBlockId`へ割り当てる |
| ドラッグ連続設置 | 生存 | セル列を縮約せず全セル送信する |
| 上り/下り坂の自動選択 | 生存 | 垂直方向から`UpBlockId`/`DownBlockId`へ割り当てる |
| 坂なしファミリーの傾斜拒否 | 生存 | 直線ブロックを割り当てて`Placeable=false`にする |
| 坂ブロックのビルドメニュー非表示 | 生存 | ファミリーの直線以外だけを非表示にする |
| 坂ブロックのスポイト | 生存 | 同ファミリーの`StraightBlockId`へ正規化する |
| 坂ブロック設置時のアンロック判定 | 生存 | 同ファミリーの直線GUIDで判定する |
| n連ブロックの設置・ロード | 廃止 | 本タスクの明示要件でマスターとPrefabを削除する |

---

### Task 1: スキーマとテストマスターを単一直線契約へ変更する

**Files:**
- Modify: `VanillaSchema/blocks.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs`

**Interfaces:**
- Produces: generated `BeltConveyorFamiliesElement.StraightBlockGuid : Guid`
- Removes: generated `BeltConveyorFamiliesElement.StraightBlocks`

- [ ] **Step 1: Write the failing schema consumer tests**

Update `BeltConveyorFamilyTest` assertions to expect one straight block:

```csharp
BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyor, out var family);
Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, family.StraightBlockId);
Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorUp, family.UpBlockId);
```

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltConveyorFamilyTest"
```

Expected: compile failure because `StraightBlockId` and generated `StraightBlockGuid` do not exist yet.

- [ ] **Step 2: Replace the schema array**

Replace the `straightBlocks` object array with:

```yaml
    # ファミリーの1セル直線コンベア
    # The family's single-cell straight conveyor
    - key: straightBlockGuid
      type: uuid
      foreignKey:
        schemaId: blocks
        foreignKeyIdPath: /data/[*]/blockGuid
        displayElementPath: /data/[*]/name
```

Change `_CompileRequester.cs`の`dummyText` to a new unique value such as `remove-multi-cell-belt-conveyors-20260719`.

- [ ] **Step 3: Mechanically migrate test JSON and delete long test blocks**

For every family, replace:

```json
"straightBlocks": [{ "blockGuid": "00000000-0000-0000-0000-000000000015" }]
```

with:

```json
"straightBlockGuid": "00000000-0000-0000-0000-000000000015"
```

Apply the same shape conversion to every other family while retaining that family's existing `[1,1,1]` member GUID.

Delete block GUIDs `00000000-0000-0000-0000-0000000000a3` and `00000000-0000-0000-0000-0000000000a4`, their item entries, all references, and `GearBeltConveyor2`/`GearBeltConveyor3` accessors.

- [ ] **Step 4: Compile to trigger Source Generator**

Run:

```bash
uloop compile --project-path ./moorestech_client
```

Expected: generated schema succeeds; handwritten code still reports references to `StraightBlocks`/old family members, establishing the migration surface.

- [ ] **Step 5: Commit the schema/data slice after Task 2 makes it buildable**

This task and Task 2 are committed together because deleting a generated property cannot compile independently.

---

### Task 2: ファミリードメインモデルとバリデーターを単一セル化する

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorFamily.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorPlaceFamilyUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/BeltConveyorFamilyValidator.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltConveyorFamilyTest.cs`

**Interfaces:**
- Produces: `BeltConveyorFamily(BlockId straightBlockId, BlockId? upBlockId, BlockId? downBlockId)`
- Produces: `public readonly BlockId StraightBlockId`
- Produces: `public bool IsSlopeBlock(BlockId blockId)`
- Keeps: `TryGetFamily(BlockId, out BeltConveyorFamily)` and `TryGetFamilyByGuid(Guid, out BeltConveyorFamily)`

- [ ] **Step 1: Rewrite tests around the single-cell invariant**

Tests must cover:

```csharp
Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, family.StraightBlockId);
Assert.IsTrue(family.IsSlopeBlock(ForUnitTestModBlockId.TestGearBeltConveyorUp));
Assert.IsFalse(family.IsSlopeBlock(ForUnitTestModBlockId.GearBeltConveyor));
StringAssert.Contains("blockSize must be [1,1,1]", logsForTwoCellStraight);
StringAssert.Contains("belongs to more than one family", logsForDuplicatedGuid);
StringAssert.Contains("is not a belt block", logsForNonBeltGuid);
```

- [ ] **Step 2: Implement the minimal family model**

Use:

```csharp
public class BeltConveyorFamily
{
    public readonly BlockId StraightBlockId;
    public readonly BlockId? UpBlockId;
    public readonly BlockId? DownBlockId;

    public BeltConveyorFamily(BlockId straightBlockId, BlockId? upBlockId, BlockId? downBlockId)
    {
        StraightBlockId = straightBlockId;
        UpBlockId = upBlockId;
        DownBlockId = downBlockId;
    }

    public bool IsSlopeBlock(BlockId blockId)
    {
        return blockId == UpBlockId || blockId == DownBlockId;
    }
}
```

`BeltConveyorPlaceFamilyUtil` resolves `element.StraightBlockGuid` directly and removes list construction, sorting, length derivation, and `InvalidOperationException`.

- [ ] **Step 3: Enforce all family members as one cell**

Validator logic:

```csharp
familyLogs += ValidateMember(family.StraightBlockGuid, "straightBlockGuid");
familyLogs += ValidateOptionalMember(family.UpBlockGuid, "upBlockGuid");
familyLogs += ValidateOptionalMember(family.DownBlockGuid, "downBlockGuid");
```

Every resolved member must be a belt block and have `BlockSize` exactly `(1,1,1)`. Continue using the shared `seenMemberGuids` so duplicate membership and family omission remain load errors.

- [ ] **Step 4: Run focused tests**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltConveyorFamilyTest"
```

Expected: all `BeltConveyorFamilyTest` tests pass.

- [ ] **Step 5: Commit**

```bash
git add VanillaSchema/blocks.yml \
  moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs \
  moorestech_server/Assets/Scripts/Core.Master/Validator/BeltConveyorFamilyValidator.cs \
  moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorFamily.cs \
  moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorPlaceFamilyUtil.cs \
  moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest \
  moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs \
  moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltConveyorFamilyTest.cs \
  moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json
git commit -m "refactor: ベルトファミリーを単一セル化"
```

---

### Task 3: クライアント配置をセル単位のブロック割当に置換する

**Files:**
- Move through Unity: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorRunDecomposer.cs` to `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorCellBlockResolver.cs`
- Move through Unity: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorRunDecomposerTest.cs` to `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorCellBlockResolverTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs`

**Interfaces:**
- Produces: `BeltConveyorCellBlockResolver.Resolve(IReadOnlyList<PlaceInfo> cells, BeltConveyorFamily family) : List<PlaceInfo>`
- Consumes: `BeltConveyorFamily.StraightBlockId`, `UpBlockId`, `DownBlockId`

- [ ] **Step 1: Write focused failing resolver tests**

First use `AssetDatabase.MoveAsset` for both source and test paths so Unity preserves their existing `.meta` GUIDs. Rename the classes after the Editor-managed move.

Create tests for:

```csharp
var result = BeltConveyorCellBlockResolver.Resolve(cells, Family);
Assert.AreEqual(cells.Count, result.Count);
Assert.IsTrue(result.Where(x => x.VerticalDirection == BlockVerticalDirection.Horizontal)
    .All(x => x.BlockId == StraightBlock));
Assert.AreEqual(UpBlock, result.Single(x => x.VerticalDirection == BlockVerticalDirection.Up).BlockId);
Assert.AreEqual(DownBlock, result.Single(x => x.VerticalDirection == BlockVerticalDirection.Down).BlockId);
```

Also assert the original position, direction, vertical direction, and `Placeable` survive unchanged, except a missing slope makes that cell unplaceable.

- [ ] **Step 2: Run tests to verify failure**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltConveyorCellBlockResolverTest"
```

Expected: FAIL because the resolver does not exist.

- [ ] **Step 3: Implement one-to-one mapping**

Implement:

```csharp
public static List<PlaceInfo> Resolve(IReadOnlyList<PlaceInfo> cells, BeltConveyorFamily family)
{
    var result = new List<PlaceInfo>(cells.Count);
    foreach (var cell in cells) result.Add(ResolveCell(cell));
    return result;

    #region Internal

    PlaceInfo ResolveCell(PlaceInfo cell)
    {
        var blockId = family.StraightBlockId;
        var placeable = cell.Placeable;
        if (cell.VerticalDirection == BlockVerticalDirection.Up)
            ResolveSlope(family.UpBlockId, ref blockId, ref placeable);
        if (cell.VerticalDirection == BlockVerticalDirection.Down)
            ResolveSlope(family.DownBlockId, ref blockId, ref placeable);
        return new PlaceInfo
        {
            Position = cell.Position,
            Direction = cell.Direction,
            VerticalDirection = cell.VerticalDirection,
            Placeable = placeable,
            BlockId = blockId,
        };
    }

    #endregion
}
```

Keep the implementation under 200 lines and include paired Japanese/English section comments without explaining self-evident assignments.

- [ ] **Step 4: Switch the placement system**

Replace:

```csharp
_currentPlaceInfos = BeltConveyorRunDecomposer.Decompose(cellInfos, family);
```

with:

```csharp
_currentPlaceInfos = BeltConveyorCellBlockResolver.Resolve(cellInfos, family);
```

Update class comments to describe per-cell family block assignment, not length variants.

- [ ] **Step 5: Run focused tests and commit**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltConveyor(CellBlockResolver|PlacePointCalculator|CostPreviewMarker)Test"
```

Expected: all matching tests pass.

Commit:

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor \
  moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor
git commit -m "refactor: ベルト配置を1セル単位に変更"
```

---

### Task 4: ファミリー利用側から代表・長尺語彙を除去する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuEntryCatalog.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlacementPick/BlockPickResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlockPickResolverTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceBlockProtocolBeltFamilyTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceBlockProtocolTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/RemoveBlockRefundTest.cs`

**Interfaces:**
- Produces: `BeltConveyorPlaceFamilyUtil.IsSlopeBlock(Guid blockGuid) : bool`
- Uses: `family.StraightBlockId` as normalization target

- [ ] **Step 1: Rewrite behavior tests**

Change block-pick and protocol tests to use `TestGearBeltConveyorUp` or `TestGearBeltConveyorDown` as the non-straight family member:

```csharp
Assert.IsTrue(BlockPickResolver.TryResolvePickTarget(
    ForUnitTestModBlockId.TestGearBeltConveyorUp, unlockState, out var resolved));
Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, resolved.BlockId);
```

Delete the long-belt refund test. Keep the ordinary required-items refund test and add/retain slope-family unlock coverage in `PlaceBlockProtocolBeltFamilyTest`.

- [ ] **Step 2: Rename hidden-variant semantics**

Implement:

```csharp
public static bool IsSlopeBlock(Guid blockGuid)
{
    return TryGetFamilyByGuid(blockGuid, out var family) &&
           family.IsSlopeBlock(MasterHolder.BlockMaster.GetBlockId(blockGuid));
}
```

Build menu filtering becomes `.Where(b => !BeltConveyorPlaceFamilyUtil.IsSlopeBlock(b.BlockGuid))`.

- [ ] **Step 3: Normalize pick and unlock to straight**

In `BlockPickResolver` and `PlaceBlockProtocol`, replace `RepresentativeBlockId` with `StraightBlockId`. Comments must describe slope-to-straight family normalization rather than representative variants.

- [ ] **Step 4: Run focused tests**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(BlockPickResolverTest|PlaceBlockProtocolBeltFamilyTest|PlaceBlockProtocolTest|RemoveBlockRefundTest)"
```

Expected: all matching tests pass.

- [ ] **Step 5: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI \
  moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlockPickResolverTest.cs \
  moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs \
  moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest
git commit -m "refactor: ベルトファミリー利用側から長尺概念を削除"
```

---

### Task 5: 全世代マスターと移行ツールからn連を削除する

**Files:**
- Modify: `../moorestech_master/server/mods/moorestechAlphaMod_3/master/blocks.json`
- Modify: `../moorestech_master/server_v4/mods/moorestechAlphaMod_4/master/blocks.json`
- Modify: `../moorestech_master/server_v5/mods/moorestechAlphaMod_5/master/blocks.json`
- Modify: `../moorestech_master/server_v6/mods/moorestechAlphaMod_6/master/blocks.json`
- Modify: `../moorestech_master/server_v7/mods/moorestechAlphaMod_7/master/blocks.json`
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`
- Delete: `../moorestech_master/tools/belt_variant_migration/`
- Modify after external commit: `.moorestech-external-revisions.json`

**Interfaces:**
- Consumes: required JSON field `straightBlockGuid`
- Removes: all long block GUIDs and long Prefab addresses

- [ ] **Step 1: Migrate every family**

Mechanically transform every `beltConveyorFamilies[*].straightBlocks` array by resolving each member against `data[*].blockGuid`, selecting the member whose `blockSize` equals `[1,1,1]`, and writing that exact GUID as:

```json
"straightBlockGuid": "7743a779-1d62-4b94-b306-4a0670bd8b48"
```

The shown GUID is the v8 wooden gear-belt example; retain the resolved GUID for each other family. Verify every family resolves exactly one such member and fail the migration if the count is not one.

- [ ] **Step 2: Remove current long block records and category references**

Delete the 12 v8 records whose names end in `(2連)` through `(5連)` and remove their GUID entries from `blockDestructionCategories`. Search every JSON file for all 12 GUIDs and require zero results.

- [ ] **Step 3: Delete the dedicated migration tool**

Delete `tools/belt_variant_migration/migrate.py`, `verify.py`, `connector_guids.json`, and the now-empty directory. Do not delete historical design documents unless they are executable code; they describe past connector work rather than active system behavior.

- [ ] **Step 4: Validate JSON shape and commit external repository**

Run:

```bash
find ../moorestech_master -name '*.json' -type f -print0 | xargs -0 -n1 jq empty
rg -n '"straightBlocks"|\\([2345]連\\)|BeltConveyor_Straight [2345]|gear belt conveyor [23]' ../moorestech_master/server ../moorestech_master/server_v4 ../moorestech_master/server_v5 ../moorestech_master/server_v6 ../moorestech_master/server_v7 ../moorestech_master/server_v8 ../moorestech_master/tools
```

Expected: JSON validation passes and the residual search returns no active master/tool matches.

Commit:

```bash
git -C ../moorestech_master add -A
git -C ../moorestech_master commit -m "refactor: n連ベルトコンベアをマスターから削除"
```

- [ ] **Step 5: Pin the new external revision**

Update `.moorestech-external-revisions.json` `moorestech_master.commitHash` to the exact commit from Step 4 and commit:

```bash
git add .moorestech-external-revisions.json
git commit -m "chore: moorestech_master参照を更新"
```

---

### Task 6: Unity Editor経由で長尺Prefabを削除する

**Files:**
- Delete through Unity: `moorestech_client/Assets/AddressableResources/Block/BeltConveyor_Straight 2.prefab`
- Delete through Unity: `moorestech_client/Assets/AddressableResources/Block/BeltConveyor_Straight 3.prefab`
- Delete through Unity: `moorestech_client/Assets/AddressableResources/Block/BeltConveyor_Straight 4.prefab`
- Delete through Unity: `moorestech_client/Assets/AddressableResources/Block/BeltConveyor_Straight 5.prefab`
- Delete through Unity: `moorestech_client/Assets/AddressableResources/Block/gear belt conveyor 2.prefab`
- Delete through Unity: `moorestech_client/Assets/AddressableResources/Block/gear belt conveyor 3.prefab`
- Delete through Unity: `moorestech_client/Assets/AddressableResources/Block/metal_gear belt conveyor 2.prefab`
- Delete through Unity: `moorestech_client/Assets/AddressableResources/Block/metal_gear belt conveyor 3.prefab`

**Interfaces:**
- Removes: eight long Prefab Addressable assets and their Unity-generated `.meta` files

- [ ] **Step 1: Delete assets with `AssetDatabase.DeleteAsset`**

Use `uloop execute-dynamic-code` with:

```csharp
var paths = new[]
{
    "Assets/AddressableResources/Block/BeltConveyor_Straight 2.prefab",
    "Assets/AddressableResources/Block/BeltConveyor_Straight 3.prefab",
    "Assets/AddressableResources/Block/BeltConveyor_Straight 4.prefab",
    "Assets/AddressableResources/Block/BeltConveyor_Straight 5.prefab",
    "Assets/AddressableResources/Block/gear belt conveyor 2.prefab",
    "Assets/AddressableResources/Block/gear belt conveyor 3.prefab",
    "Assets/AddressableResources/Block/metal_gear belt conveyor 2.prefab",
    "Assets/AddressableResources/Block/metal_gear belt conveyor 3.prefab",
};
foreach (var path in paths)
{
    if (!UnityEditor.AssetDatabase.DeleteAsset(path))
        UnityEngine.Debug.LogError($"Failed to delete {path}");
}
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();
```

- [ ] **Step 2: Verify files and Addressable references are gone**

Run:

```bash
for name in "BeltConveyor_Straight 2" "BeltConveyor_Straight 3" "BeltConveyor_Straight 4" "BeltConveyor_Straight 5" "gear belt conveyor 2" "gear belt conveyor 3" "metal_gear belt conveyor 2" "metal_gear belt conveyor 3"; do
  test ! -e "moorestech_client/Assets/AddressableResources/Block/$name.prefab"
  test ! -e "moorestech_client/Assets/AddressableResources/Block/$name.prefab.meta"
done
rg -n 'BeltConveyor_Straight [2345]|gear belt conveyor [23]' moorestech_client/Assets/AddressableAssetsData moorestech_client/Assets/AddressableResources
```

Expected: all `test` checks pass and no long Prefab Addressable reference remains.

- [ ] **Step 3: Commit**

```bash
git add -A moorestech_client/Assets/AddressableResources/Block moorestech_client/Assets/AddressableAssetsData
git commit -m "chore: n連ベルトコンベアPrefabを削除"
```

---

### Task 7: 全体QAと完了監査

**Files:**
- Inspect: all modified files
- Modify only if QA finds defects

- [ ] **Step 1: Search for forbidden active concepts**

Run:

```bash
rg -n 'StraightVariantsDesc|RepresentativeBlockId|BeltConveyorRunDecomposer|straightBlocks|GearBeltConveyor[23]|長尺ベルト|[2345]連ベルト|BeltConveyor_Straight [2345]|gear belt conveyor [23]' \
  VanillaSchema moorestech_server moorestech_client mooresmaster ../moorestech_master \
  --glob '!docs/**' --glob '!design/**' --glob '!Library/**' --glob '!Temp/**'
```

Expected: no active code, schema, master, test, tool, or Prefab matches.

- [ ] **Step 2: Check C# file limits and forbidden declarations**

Run:

```bash
find moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts -name '*.cs' -type f -print0 |
  xargs -0 wc -l | awk '$1 > 200 && $2 != "total" {print}'
git diff HEAD~6 --name-only -- '*.cs' | while IFS= read -r file; do
  test -f "$file" && rg -n '\\bpartial\\b' "$file"
done
git diff --check
```

Expected: modified C# files are at most 200 lines, contain no `partial`, and diff check passes.

- [ ] **Step 3: Compile**

Run:

```bash
uloop compile --project-path ./moorestech_client
```

Expected: compilation succeeds with zero errors.

- [ ] **Step 4: Run focused regression tests**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(BeltConveyorFamilyTest|BeltConveyorCellBlockResolverTest|BlockPickResolverTest|PlaceBlockProtocolBeltFamilyTest|PlaceBlockProtocolTest|RemoveBlockRefundTest|BeltConveyorPlacePointCalculatorTest|BeltConveyorCostPreviewMarkerTest)"
```

Expected: all matching tests pass.

- [ ] **Step 5: Run the full client test suite**

Run:

```bash
uloop run-tests --project-path ./moorestech_client
```

Expected: all client-imported server and client tests pass.

- [ ] **Step 6: Inspect Unity errors**

Run:

```bash
uloop get-logs --project-path ./moorestech_client --log-type Error
```

Expected: no errors caused by this change.

- [ ] **Step 7: Final status and commit**

Run:

```bash
git status --short
git -C ../moorestech_master status --short
git log --oneline --decorate -8
git -C ../moorestech_master log --oneline --decorate -3
```

Fix every discovered defect, rerun the affected checks, and commit all remaining task-related changes:

```bash
git add -A
git commit -m "test: n連ベルト削除のQA指摘を修正"
```

Expected: both worktrees are clean and all requested behavior is proven by current-state searches, compilation, and tests.
