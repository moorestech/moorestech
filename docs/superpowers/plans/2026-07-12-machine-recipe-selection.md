# 機械レシピ任意選択（選択必須化） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 機械のレシピ決定を「インベントリからの自動判定」から「プレイヤーの明示選択（選択必須）」へ置き換える。

**Architecture:** 選択レシピは `MachineProcessContext`（Vanilla/CleanRoom両プロセッサ共有の内部コンテキスト）が保持し、両プロセッサが `IMachineRecipeSelectorComponent` を実装して変更を受ける。変更は新プロトコル `va:machineRecipeSelection`（Operation分岐1本）経由。選択状態のクライアント同期は既存の `MachineBlockStateDetail`（ブロック状態同期チャネル）にフィールド追加して行い、Get専用オペレーションは作らない。加工中の変更は「アイテム全量収容シミュレーション→ジョブ中断→返却」の原子的フロー（液体はベストエフォート返却・溢れ消失）。

**Tech Stack:** C# / Unity / MessagePack(通信) / Newtonsoft JSON(セーブ) / UniRx / uloop CLI

**Spec:** `docs/superpowers/specs/2026-07-10-machine-recipe-selection-design.md`

## Global Constraints

- partial禁止・1ファイル200行以下・1ディレクトリ10ファイル以下・try-catch原則禁止
- イベントはUniRx（`Subject<T>` + `IObservable<T>`）。C# `event Action` 禁止
- 永続化はGUID文字列＋Newtonsoft JSON。揮発int（ItemId/BlockId）・MessagePackの保存禁止
- デフォルト引数禁止。引数追加時は呼び出し側を全て更新
- コメントは日本語・英語の2行セット（約3〜10行ごと、自明なものは書かない）
- .csファイル変更後は必ず `uloop compile --project-path ./moorestech_client` を実行
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`
- ドメインリロードエラー時は45秒待ってリトライ
- Prefab編集はテキスト直編集禁止。`uloop execute-dynamic-code` のみ
- .metaファイルは手動作成しない
- 各タスク完了時に必ずコミット

## 配置と前例（spec-architecture-review済み）

| 決定 | 前例（ファイルパス） |
|---|---|
| 選択状態は `MachineProcessContext` に保持（実行時状態はドメイン実装層） | `Game.Block/Blocks/Machine/State/MachineProcessContext.cs` の共有状態群 |
| 公開interfaceは `Game.Block.Interface/Component/` | `IBlockBlueprintSettings.cs`（同ディレクトリ） |
| プロトコルはOperation分岐1本＋static factory | `Server.Protocol/PacketResponse/FilterSplitterStateProtocol.cs` |
| interfaceでのComponent取得 | `TrainUnitStationDocking.cs:238` の `TryGetComponent<ITrainDockingReceiver>` |
| 選択状態の同期は既存ブロック状態チャネル拡張（新規Get/Applierを作らない） | `MachineBlockStateDetail` は既に `MachineRecipeGuid` を同期済み |
| Blueprint対応はGet/Apply対の専用小コンポーネント | `VanillaFilterSplitterComponent` の `IBlockBlueprintSettings` 実装 |
| 返却シミュレーションは既存インベントリサービスのコピーで行う（再実装しない） | `Core.Inventory/OpenableInventoryItemDataStoreService.cs` |
| クライアントのレシピ候補は共有マスタ＋同期済みアンロック状態から導出 | `ItemRecipeViewerDataContainer.cs` / DI登録 `MainGameStarter.cs:252` |
| プレイヤーID・インベントリ | `PlaceBlockProtocol.cs`（`IPlayerInventoryDataStore`）/ クライアントは `ClientContext.PlayerConnectionSetting.PlayerId` |

**機能死活表**（この計画で死ぬ操作は自動加工のみ＝spec裁定済み）:
- 機械の自動加工 → **廃止**（ユーザー裁定: 選択必須化）
- 機械UIの産出レート・進捗・電力表示 → 生きる（`MachineBlockStateDetail`/`CommonMachineBlockStateDetail` 継続）
- 図鑑レシピビューア → 変更なし
- CleanRoom機械のHalted凍結・チップ抽選 → 生きる（状態機械は不変、Idle判定の入力源のみ変更）
- 既存セーブ → 読み込み可（選択フィールド無し＝未選択。加工中ジョブは完走後に停止）

## File Structure

```
moorestech_server/Assets/Scripts/
  Core.Master/
    MachineRecipesMaster.cs                     # 変更: 入力キー辞書を削除、GUID辞書化
    Validator/MachineRecipesMasterUtil.cs       # 変更: Initialize/GetRecipeElementKey削除（Validateのみ残す）
  Game.Block.Interface/
    Component/IMachineRecipeSelectorComponent.cs  # 新規: 選択interface + 結果enum
    State/MachineBlockStateDetail.cs            # 変更: SelectedRecipeGuid追加
  Game.Block/Blocks/Machine/
    MachineRecipeMaster.cs                      # 変更: TryGetRecipeElement削除（RecipeConfirmation残す）
    VanillaMachineProcessorComponent.cs         # 変更: interface実装・選択変更フロー（200行制約のため下2ファイルを分離）
    ProcessState.cs                             # 新規: enum + ToStr拡張（Processorから移動）
    VanillaMachineProcessorSaveJsonObject.cs    # 新規: セーブJSON（Processorから移動）+ selectedRecipeGuid
    Inventory/VanillaMachineInputInventory.cs   # 変更: 自動探索削除、BlockId/IsRecipeUnlocked公開
    State/MachineProcessContext.cs              # 変更: SelectedRecipe追加
    State/IdleMachineProcessState.cs            # 変更: 選択レシピ充足判定へ
    State/ProcessingMachineProcessState.cs      # 変更: CurrentRecipe/CancelProcessing追加
    RecipeSelection/MachineRecipeSelectionUtil.cs      # 新規: 検証+中断返却の共通フロー
    RecipeSelection/MachineRecipeRefundUtil.cs         # 新規: 返却シミュレーション/実行
    RecipeSelection/MachineRecipeBlueprintSettingsComponent.cs  # 新規: Blueprint Get/Apply
  Game.Block/Blocks/CleanRoom/Machine/
    CleanRoomMachineProcessorComponent.cs       # 変更: interface実装（チップ抽選をUtilへ分離し行数確保）
    CleanRoomChipDrawApplyUtil.cs               # 新規: チップ抽選置換（Processorから移動）
    CleanRoomMachineProcessorSaveState.cs       # 変更: selectedRecipeGuid保存/復元
  Game.Block/Factory/BlockTemplate/
    BlockTemplateUtil.cs                        # 変更: MachineLoadStateで選択復元
    VanillaMachineTemplate.cs                   # 変更: Blueprintコンポーネント追加
    VanillaGearMachineTemplate.cs               # 変更: 同上
    (CleanRoom機械のTemplate)                    # 変更: 同上（実ファイル名は実装時にgrepで特定）
  Server.Protocol/PacketResponse/
    MachineRecipeSelectionProtocol.cs           # 新規
    PacketResponseCreator.cs                    # 変更: 登録1行
  Tests/
    Util/MachineRecipeSelectTestUtil.cs         # 新規: テスト用選択ヘルパー
    CombinedTest/Core/MachineRecipeSelectionTest.cs        # 新規
    CombinedTest/Core/MachineRecipeChangeRefundTest.cs     # 新規
    CombinedTest/Server/PacketTest/MachineRecipeSelectionProtocolTest.cs  # 新規
    （既存の自動判定前提テスト多数を修正）

moorestech_client/Assets/Scripts/
  Client.Network/API/VanillaApiWithResponse.cs  # 変更: メソッド1本追加
  Client.Game/InGame/UI/Inventory/Block/
    MachineRecipeSelectionPanel.cs              # 新規: レシピ一覧パネル
    MachineBlockInventoryView.cs                # 変更: パネル接続・未設定表示
moorestech_client/Assets/AddressableResources/UI/Block/
  MachineBlockInventory.prefab                  # uloop経由で変更
  GearMachineBlockInventory.prefab              # uloop経由で変更
```

---

### Task 1: Core.Master — 入力キー辞書の撤去とGUID辞書化

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Core.Master/MachineRecipesMaster.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/MachineRecipesMasterUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Block/MachineRecipeConfigTest.cs`
- Modify: ForUnitTestモッドの機械レシピJSON（重複入力レシピ・ロックレシピ追加）

**Interfaces:**
- Consumes: なし（起点タスク）
- Produces: `MachineRecipesMaster.GetRecipeElement(Guid machineRecipeGuid)` が辞書引きで `MachineRecipeMasterElement`（見つからなければ null）を返す。`TryGetRecipeElement(BlockId, List<ItemId>, List<FluidId>, out ...)` は**削除される**（Task 2 が呼び出し側を削除）

- [ ] **Step 1: テストモッドに「同一入力で別出力」のレシピと「initialUnlocked:false」のレシピを追加**

まず対象JSONを特定して現物を確認する:

```bash
find moorestech_server/Assets/Scripts/Tests.Module -iname "*machineRecipe*"
```

見つかった `machineRecipes.json` の `data` 配列に2レシピを追加する。既存レシピから1つ選び（`blockGuid`・`inputItems` をそのままコピー）、出力だけ別アイテムに変えたレシピ（旧実装ならキー重複例外になる構成）を1つ、および `"initialUnlocked": false` のレシピを1つ追加する。`machineRecipeGuid` は新規UUID（`uuidgen | tr A-Z a-z`）を使い、既存GUIDと重複しないこと。追加したGUID2つはテストコードから参照するのでメモしておく。

- [ ] **Step 2: 失敗するテストを書く（MachineRecipeConfigTest を書き換え）**

`MachineRecipeConfigTest.cs` を読み、入力キー検索（`TryGetRecipeElement(blockId, itemIds, fluidIds, ...)`）を検証しているテストメソッドを削除し、以下を追加する（クラスの既存の初期化パターンに合わせる）:

```csharp
[Test]
public void GetRecipeElementByGuidTest()
{
    var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

    // 全レシピがGUIDで一意に引けること
    // Every recipe is retrievable by its GUID
    foreach (var recipe in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
    {
        Assert.AreEqual(recipe, MasterHolder.MachineRecipesMaster.GetRecipeElement(recipe.MachineRecipeGuid));
    }

    // 存在しないGUIDはnull
    // Unknown GUID returns null
    Assert.IsNull(MasterHolder.MachineRecipesMaster.GetRecipeElement(System.Guid.NewGuid()));
}

[Test]
public void DuplicateInputRecipesAreAllowedTest()
{
    // Step 1で追加した同一入力レシピが存在してもマスタロードが例外を投げないこと
    // Master load must not throw even when two recipes share the same block and inputs
    Assert.DoesNotThrow(() => new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)));
}
```

- [ ] **Step 3: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeConfigTest"`
Expected: `DuplicateInputRecipesAreAllowedTest` が旧Initializeのキー重複例外でFAIL

- [ ] **Step 4: MachineRecipesMaster をGUID辞書化**

`MachineRecipesMaster.cs` 全体を以下に置き換える:

```csharp
using System;
using System.Collections.Generic;
using Core.Master.Validator;
using Mooresmaster.Loader.MachineRecipesModule;
using Mooresmaster.Model.MachineRecipesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class MachineRecipesMaster : IMasterValidator
    {
        public readonly MachineRecipes MachineRecipes;
        private Dictionary<Guid, MachineRecipeMasterElement> _machineRecipesByGuid;

        public MachineRecipesMaster(JToken jToken)
        {
            MachineRecipes = MachineRecipesLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
        {
            return MachineRecipesMasterUtil.Validate(MachineRecipes, out errorLogs);
        }

        public void Initialize()
        {
            // レシピ選択はGUID指定のため、GUID→レシピの辞書のみ構築する
            // Recipe selection is GUID-based, so build only the GUID-to-recipe dictionary
            _machineRecipesByGuid = new Dictionary<Guid, MachineRecipeMasterElement>();
            foreach (var recipe in MachineRecipes.Data)
            {
                _machineRecipesByGuid.Add(recipe.MachineRecipeGuid, recipe);
            }
        }

        public MachineRecipeMasterElement GetRecipeElement(Guid machineRecipeGuid)
        {
            return _machineRecipesByGuid.GetValueOrDefault(machineRecipeGuid);
        }
    }
}
```

`Validator/MachineRecipesMasterUtil.cs` から `Initialize`（キー辞書構築・重複例外）と `GetRecipeElementKey` を削除し、`Validate` のみ残す。

注意: `MachineRecipesMaster.TryGetRecipeElement` を消すとTask 2完了までコンパイルエラーになるため、このタスクでは**メソッドを残したまま中身を `recipe = null; return false;` にはせず**、呼び出し側（`Game.Block/Blocks/Machine/MachineRecipeMaster.cs` の `TryGetRecipeElement`）ごとこのタスクで削除する。`VanillaMachineInputInventory.TryGetRecipeElement` / `IsAllowedToStartProcess` / `IdleMachineProcessState` が壊れる場合は、Task 2の変更のうち「呼び出し側の削除」に必要な最小限（`VanillaMachineInputInventory.TryGetRecipeElement` の削除と `IsAllowedToStartProcess` の一時的な `return false;` 化ではなく、**Task 2のStep 5〜7を先行適用**）をここで行ってよい。ただしその場合もタスクの検証はStep 5のテストで行う。

- [ ] **Step 5: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（残るなら呼び出し側の削除漏れ。`grep -rn "GetRecipeElementKey\|TryGetRecipeElement" moorestech_server moorestech_client --include="*.cs"` で残存確認）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeConfigTest"`
Expected: PASS

- [ ] **Step 6: コミット**

```bash
git add -A && git commit -m "refactor(master): 機械レシピの入力キー辞書を撤去しGUID辞書化"
```

---

### Task 2: サーバーコア — 選択状態・Idle判定・SetSelectedRecipe（返却フロー込み）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/IMachineRecipeSelectorComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeRefundUtil.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeSelectionUtil.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/ProcessState.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorSaveJsonObject.cs`
- Modify: `Game.Block/Blocks/Machine/State/MachineProcessContext.cs`
- Modify: `Game.Block/Blocks/Machine/State/IdleMachineProcessState.cs`
- Modify: `Game.Block/Blocks/Machine/State/ProcessingMachineProcessState.cs`
- Modify: `Game.Block/Blocks/Machine/Inventory/VanillaMachineInputInventory.cs`
- Modify: `Game.Block/Blocks/Machine/MachineRecipeMaster.cs`
- Modify: `Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs`
- Modify: `Game.Block.Interface/State/MachineBlockStateDetail.cs`
- Test: `Tests/CombinedTest/Core/MachineRecipeSelectionTest.cs`（新規）

**Interfaces:**
- Consumes: `MachineRecipesMaster.GetRecipeElement(Guid)`（Task 1）
- Produces:
  - `interface IMachineRecipeSelectorComponent : IBlockComponent { Guid SelectedRecipeGuid { get; } MachineRecipeSelectionResult SetSelectedRecipe(MachineRecipeMasterElement recipe, IOpenableInventory refundOverflowInventory); MachineRecipeSelectionResult ClearSelectedRecipe(IOpenableInventory refundOverflowInventory); }`
  - `enum MachineRecipeSelectionResult { Success, RecipeBlockMismatch, RecipeLocked, RefundFailed }`
  - `MachineBlockStateDetail` に `[Key(2)] public string SelectedRecipeGuid` と `CreateState(float, Guid, Guid)`
  - `MachineProcessContext.SelectedRecipe`（internal）、`ProcessingMachineProcessState.CurrentRecipe` / `CancelProcessing()`（internal）
  - `VanillaMachineInputInventory.BlockId` / `IsRecipeUnlocked(Guid)` / `IsAllowedToStartProcess(MachineRecipeMasterElement)`

- [ ] **Step 1: 失敗するテストを書く**

`Tests/CombinedTest/Core/MachineRecipeSelectionTest.cs` を新規作成（creating-server-testsスキルの初期化パターン準拠）:

```csharp
using System;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Core.Update;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    // レシピ選択必須化の基本挙動を検証する
    // Verifies the core behavior of mandatory recipe selection
    public class MachineRecipeSelectionTest
    {
        [Test]
        public void NoSelectionNeverProcessesTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 電気機械のレシピを1つ選び、材料だけを投入する（選択はしない）
            // Pick an electric machine recipe and insert only its inputs without selecting it
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }

            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            for (var i = 0; i < 10; i++) GameUpdater.UpdateWithWait();

            // 未選択のため加工は始まらない
            // Processing must not start without a selection
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
        }

        [Test]
        public void SelectedRecipeProcessesTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            // レシピを選択してから材料を投入
            // Select the recipe, then insert its inputs
            Assert.IsTrue(block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector));
            var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(recipe, overflow));
            Assert.AreEqual(recipe.MachineRecipeGuid, selector.SelectedRecipeGuid);

            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }

            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            GameUpdater.UpdateWithWait();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
        }

        [Test]
        public void WrongBlockRecipeIsRejectedTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // ブロックAを設置し、別ブロックB用のレシピを設定しようとする
            // Place block A and try to set a recipe that belongs to block B
            var recipeA = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            MachineRecipeMasterElement recipeB = null;
            foreach (var r in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                if (r.BlockGuid != recipeA.BlockGuid) { recipeB = r; break; }
            }
            Assert.IsNotNull(recipeB, "テストモッドに2種類以上の機械ブロックのレシピが必要");

            var blockId = MasterHolder.BlockMaster.GetBlockId(recipeA.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);

            var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
            Assert.AreEqual(MachineRecipeSelectionResult.RecipeBlockMismatch, selector.SetSelectedRecipe(recipeB, overflow));
            Assert.AreEqual(Guid.Empty, selector.SelectedRecipeGuid);
        }

        [Test]
        public void LockedRecipeIsRejectedTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // Task 1でテストモッドに追加したinitialUnlocked:falseのレシピを対象にする
            // Target the initialUnlocked:false recipe added to the test mod in Task 1
            MachineRecipeMasterElement lockedRecipe = null;
            foreach (var r in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                if (!r.InitialUnlocked) { lockedRecipe = r; break; }
            }
            Assert.IsNotNull(lockedRecipe, "テストモッドにinitialUnlocked:falseのレシピが必要（Task 1）");

            var blockId = MasterHolder.BlockMaster.GetBlockId(lockedRecipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);

            var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
            Assert.AreEqual(MachineRecipeSelectionResult.RecipeLocked, selector.SetSelectedRecipe(lockedRecipe, overflow));
        }

        [Test]
        public void SameInputsDifferentRecipeSelectionTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // Task 1で追加した「同一入力・別出力」ペアを検出し、選択した方のレシピで加工されることを確認
            // Find the duplicate-input pair added in Task 1 and verify the selected one is used
            MachineRecipeMasterElement first = null, second = null;
            var data = MasterHolder.MachineRecipesMaster.MachineRecipes.Data;
            for (var i = 0; i < data.Length && second == null; i++)
            for (var j = i + 1; j < data.Length; j++)
            {
                if (data[i].BlockGuid != data[j].BlockGuid) continue;
                if (!SameInputs(data[i], data[j])) continue;
                first = data[i]; second = data[j];
                break;
            }
            Assert.IsNotNull(second, "テストモッドに同一入力レシピペアが必要（Task 1）");

            var blockId = MasterHolder.BlockMaster.GetBlockId(second.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);
            var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
            selector.SetSelectedRecipe(second, overflow);

            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var inputItem in second.InputItems)
            {
                blockInventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }

            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            GameUpdater.UpdateWithWait();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(second.MachineRecipeGuid, processor.RecipeGuid);

            #region Internal

            static bool SameInputs(MachineRecipeMasterElement a, MachineRecipeMasterElement b)
            {
                if (a.InputItems.Length != b.InputItems.Length) return false;
                foreach (var ia in a.InputItems)
                {
                    var found = false;
                    foreach (var ib in b.InputItems)
                    {
                        if (ia.ItemGuid == ib.ItemGuid && ia.Count == ib.Count) { found = true; break; }
                    }
                    if (!found) return false;
                }
                return true;
            }

            #endregion
        }
    }
}
```

注意: 電気機械は電力供給が無いと加工が進まないが、Idle→Processingの遷移自体は電力不要（`GetNextUpdate` は材料充足のみ見る）。遷移しない場合は `GearMachineIoTest.cs` の電力供給パターン（`VanillaElectricMachineComponent.SupplyEnergy`）を参照して補う。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionTest"`
Expected: コンパイルエラー（`IMachineRecipeSelectorComponent` 未定義）

- [ ] **Step 3: interfaceと結果enumを作成**

`Game.Block.Interface/Component/IMachineRecipeSelectorComponent.cs`:

```csharp
using System;
using Core.Inventory;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     機械のレシピ選択を受け付けるコンポーネント。未選択の機械は加工しない
    ///     Accepts machine recipe selection; machines without a selection never process
    /// </summary>
    public interface IMachineRecipeSelectorComponent : IBlockComponent
    {
        Guid SelectedRecipeGuid { get; }

        // 加工中の変更はジョブを中断し消費済み材料を返却する。入力に収まらない分はrefundOverflowInventoryへ
        // Changing mid-processing cancels the job and refunds consumed inputs; overflow goes to refundOverflowInventory
        MachineRecipeSelectionResult SetSelectedRecipe(MachineRecipeMasterElement recipe, IOpenableInventory refundOverflowInventory);
        MachineRecipeSelectionResult ClearSelectedRecipe(IOpenableInventory refundOverflowInventory);
    }

    public enum MachineRecipeSelectionResult
    {
        Success,
        RecipeBlockMismatch,
        RecipeLocked,
        RefundFailed,
    }
}
```

- [ ] **Step 4: コンテキストと加工ステートの変更**

`MachineProcessContext.cs` にフィールド追加（using に `Mooresmaster.Model.MachineRecipesModule` を追加）:

```csharp
        // プレイヤーが選択したレシピ。未選択はnullで、その間は加工しない
        // Recipe selected by the player; null means unselected and the machine never processes
        public MachineRecipeMasterElement SelectedRecipe;
```

`ProcessingMachineProcessState.cs` に追加:

```csharp
        // 進行中ジョブのレシピ（返却計算用）。ジョブが無ければnull
        // Recipe of the running job (for refund calculation); null when no job exists
        public MachineRecipeMasterElement CurrentRecipe => _recipe;

        // 出力を払い出さずにジョブを破棄する（レシピ変更の返却フロー用）
        // Discard the job without paying outputs (used by the recipe-change refund flow)
        public void CancelProcessing()
        {
            _pendingOutputs = null;
            _recipe = null;
            TotalTicks = 0;
            RemainingTicks = 0;
        }
```

`IdleMachineProcessState.GetNextUpdate` を置き換え:

```csharp
        public ProcessState GetNextUpdate()
        {
            // 選択レシピが無ければ加工しない（レシピ選択必須）
            // Never process without a selected recipe (selection is mandatory)
            var recipe = _context.SelectedRecipe;
            if (recipe == null || !_context.InputInventory.IsAllowedToStartProcess(recipe))
            {
                return ProcessState.Idle;
            }

            // 抽選を開始時に確定し実スタックで容量確認
            // Fix rolls at start and check capacity with realized stacks
            var effect = _context.EffectComponent.AggregateCurrent();
            var realizedOutputs = MachineOutputFactoryUtil.CreateRealizedOutputs(recipe, effect);
            if (!_context.OutputInventory.CanStoreOutputs(realizedOutputs, MachineOutputFactoryUtil.CreateFluidOutputs(recipe)))
            {
                return ProcessState.Idle;
            }

            // 加工ジョブをProcessingStateへ渡して遷移
            // Hand the job to ProcessingState and transition
            _processingState.SetProcessing(recipe, realizedOutputs);
            return ProcessState.Processing;
        }
```

- [ ] **Step 5: VanillaMachineInputInventory から自動探索を削除し公開APIを追加**

`TryGetRecipeElement` を削除し、`IsAllowedToStartProcess` を差し替え、以下を追加:

```csharp
        public BlockId BlockId => _blockId;

        public bool IsAllowedToStartProcess(MachineRecipeMasterElement recipe)
        {
            // 選択済みレシピの材料充足のみを確認する（レシピ探索は行わない）
            // Only verify the selected recipe's inputs are satisfied (no recipe search)
            return recipe.RecipeConfirmation(_blockId, InputSlot, FluidInputSlot);
        }

        public bool IsRecipeUnlocked(Guid machineRecipeGuid)
        {
            return _gameUnlockStateData.MachineRecipeUnlockStateInfos.TryGetValue(machineRecipeGuid, out var info) && info.IsUnlocked;
        }
```

`Game.Block/Blocks/Machine/MachineRecipeMaster.cs` から `TryGetRecipeElement` を削除（`RecipeConfirmation` 拡張メソッドは残す）。usingの整理を忘れない。

- [ ] **Step 6: 返却ユーティリティを作成**

`Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeRefundUtil.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Context;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.RecipeSelection
{
    /// <summary>
    ///     加工中断時の消費済み材料の返却。アイテムは全量収容シミュレーション後に実行、液体はベストエフォート
    ///     Refunds consumed inputs on job cancel; items are simulated first, fluids are best-effort
    /// </summary>
    public static class MachineRecipeRefundUtil
    {
        // 返却対象のアイテム材料を生成する（isRemainは消費されていないため対象外）
        // Build the item stacks to refund (isRemain inputs are not consumed, so excluded)
        public static List<IItemStack> CreateRefundStacks(MachineRecipeMasterElement recipe)
        {
            var stacks = new List<IItemStack>();
            foreach (var input in recipe.InputItems)
            {
                if (input.IsRemain.HasValue && input.IsRemain.Value) continue;
                var itemId = MasterHolder.ItemMaster.GetItemId(input.ItemGuid);
                stacks.Add(ServerContext.ItemStackFactory.Create(itemId, input.Count));
            }
            return stacks;
        }

        // 入力インベントリ→溢れ先の順で全量収容できるか（アイテムのみ。液体は判定対象外）
        // Whether all item refunds fit into the input inventory then the overflow inventory (fluids excluded)
        public static bool CanRefundAllItems(VanillaMachineInputInventory input, IOpenableInventory overflow, List<IItemStack> refunds)
        {
            var inputRemainder = CopyMachineInput(input).InsertItem(refunds);
            var overflowRemainder = CopyOverflow(overflow).InsertItem(FilterNonEmpty(inputRemainder));
            return FilterNonEmpty(overflowRemainder).Count == 0;
        }

        public static void ExecuteRefund(VanillaMachineInputInventory input, IOpenableInventory overflow, List<IItemStack> refunds, MachineRecipeMasterElement recipe)
        {
            // シミュレーションと同じ順で実挿入する（入力→溢れ先）
            // Insert for real in the same order as the simulation (input first, then overflow)
            var remainder = input.InsertItem(refunds);
            overflow.InsertItem(FilterNonEmpty(remainder));
            RefundFluidsBestEffort(input, recipe);
        }

        #region Internal

        // 機械入力インベントリの挿入規則（同一アイテム複数スタック禁止）を再現したコピー
        // Copy that mirrors the machine input insertion rule (no multiple stacks per item)
        private static OpenableInventoryItemDataStoreService CopyMachineInput(VanillaMachineInputInventory input)
        {
            var option = new OpenableInventoryItemDataStoreServiceOption { AllowMultipleStacksPerItemOnInsert = false };
            var sim = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, input.InputSlot.Count, option);
            for (var i = 0; i < input.InputSlot.Count; i++) sim.SetItemWithoutEvent(i, input.InputSlot[i]);
            return sim;
        }

        private static OpenableInventoryItemDataStoreService CopyOverflow(IOpenableInventory overflow)
        {
            var sim = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, overflow.GetSlotSize());
            for (var i = 0; i < overflow.GetSlotSize(); i++) sim.SetItemWithoutEvent(i, overflow.GetItem(i));
            return sim;
        }

        private static List<IItemStack> FilterNonEmpty(List<IItemStack> stacks)
        {
            var result = new List<IItemStack>();
            foreach (var stack in stacks)
            {
                if (stack.Id != ItemMaster.EmptyItemId && stack.Count > 0) result.Add(stack);
            }
            return result;
        }

        // 液体は入力タンクへ戻せる分だけ戻し、収まらない分は消失させる（液体はインベントリで扱えないため）
        // Fluids go back to input tanks as far as capacity allows; the overflow is lost (no fluid inventory exists)
        private static void RefundFluidsBestEffort(VanillaMachineInputInventory input, MachineRecipeMasterElement recipe)
        {
            foreach (var inputFluid in recipe.InputFluids)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid);
                var remaining = inputFluid.Amount;
                foreach (var container in input.FluidInputSlot)
                {
                    if (remaining <= 0) break;
                    if (container.FluidId != fluidId && container.FluidId != FluidMaster.EmptyFluidId) continue;

                    var addable = Math.Min(remaining, container.Capacity - container.Amount);
                    if (addable <= 0) continue;
                    container.FluidId = fluidId;
                    container.Amount += addable;
                    remaining -= addable;
                }
            }
        }

        #endregion
    }
}
```

`RecipeSelection/MachineRecipeSelectionUtil.cs`:

```csharp
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.State;
using Game.Block.Interface.Component;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.RecipeSelection
{
    /// <summary>
    ///     レシピ選択の検証と、進行中ジョブの中断・返却の共通フロー（通常機械/クリーンルーム機械で共用）
    ///     Shared validation and cancel-with-refund flow for recipe selection (vanilla and clean-room machines)
    /// </summary>
    internal static class MachineRecipeSelectionUtil
    {
        public static MachineRecipeSelectionResult ValidateSelection(VanillaMachineInputInventory inputInventory, MachineRecipeMasterElement recipe)
        {
            // レシピが自ブロックのものであること・アンロック済みであることをサーバー側で保証する
            // Server-side guarantees: the recipe belongs to this block and is unlocked
            if (MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid) != inputInventory.BlockId) return MachineRecipeSelectionResult.RecipeBlockMismatch;
            if (!inputInventory.IsRecipeUnlocked(recipe.MachineRecipeGuid)) return MachineRecipeSelectionResult.RecipeLocked;
            return MachineRecipeSelectionResult.Success;
        }

        // 進行中ジョブがあれば返却して中断する。アイテムが全量収容できなければfalse（変更自体を中止）
        // Cancel a running job with refund; returns false when items cannot be fully stored (abort the change)
        public static bool TryCancelRunningJobWithRefund(VanillaMachineInputInventory inputInventory, ProcessingMachineProcessState processingState, IOpenableInventory refundOverflowInventory)
        {
            var runningRecipe = processingState.CurrentRecipe;
            if (runningRecipe == null) return true;

            var refunds = MachineRecipeRefundUtil.CreateRefundStacks(runningRecipe);
            if (!MachineRecipeRefundUtil.CanRefundAllItems(inputInventory, refundOverflowInventory, refunds)) return false;

            MachineRecipeRefundUtil.ExecuteRefund(inputInventory, refundOverflowInventory, refunds, runningRecipe);
            processingState.CancelProcessing();
            return true;
        }
    }
}
```

注意: `ProcessingMachineProcessState` と `MachineProcessContext` は `internal` のため、このUtilも `internal`（同一アセンブリ内でのみ使用）。

- [ ] **Step 7: VanillaMachineProcessorComponent の変更（200行制約のためファイル分割込み）**

1. `ProcessState` enum と `ProcessStateExtension` を `Game.Block/Blocks/Machine/ProcessState.cs` へ移動（namespace `Game.Block.Blocks.Machine` のまま。中身は現行コードをそのまま移す）。
2. `VanillaMachineProcessorSaveJsonObject` を `Game.Block/Blocks/Machine/VanillaMachineProcessorSaveJsonObject.cs` へ移動（同上）。
3. `VanillaMachineProcessorComponent` に interface 実装を追加:

```csharp
    public class VanillaMachineProcessorComponent : IBlockStateObservable, IUpdatableBlockComponent, IMachineRecipeSelectorComponent
```

```csharp
        public Guid SelectedRecipeGuid => _context.SelectedRecipe?.MachineRecipeGuid ?? Guid.Empty;

        public MachineRecipeSelectionResult SetSelectedRecipe(MachineRecipeMasterElement recipe, IOpenableInventory refundOverflowInventory)
        {
            BlockException.CheckDestroy(this);

            var validation = MachineRecipeSelectionUtil.ValidateSelection(_context.InputInventory, recipe);
            if (validation != MachineRecipeSelectionResult.Success) return validation;

            // 同一レシピの再設定はジョブを中断しないno-op
            // Re-selecting the same recipe is a no-op that never cancels the job
            if (recipe.MachineRecipeGuid == SelectedRecipeGuid) return MachineRecipeSelectionResult.Success;

            return ChangeSelection(recipe, refundOverflowInventory);
        }

        public MachineRecipeSelectionResult ClearSelectedRecipe(IOpenableInventory refundOverflowInventory)
        {
            BlockException.CheckDestroy(this);
            if (_context.SelectedRecipe == null) return MachineRecipeSelectionResult.Success;
            return ChangeSelection(null, refundOverflowInventory);
        }

        private MachineRecipeSelectionResult ChangeSelection(MachineRecipeMasterElement recipe, IOpenableInventory refundOverflowInventory)
        {
            // 進行中ジョブは返却して中断する。返却しきれなければ変更自体を中止する
            // Cancel the running job with refund; abort the whole change when the refund does not fit
            if (!MachineRecipeSelectionUtil.TryCancelRunningJobWithRefund(_context.InputInventory, _processingState, refundOverflowInventory))
            {
                return MachineRecipeSelectionResult.RefundFailed;
            }

            if (CurrentState == ProcessState.Processing) CurrentState = ProcessState.Idle;
            _context.SelectedRecipe = recipe;
            _changeState.OnNext(Unit.Default);
            return MachineRecipeSelectionResult.Success;
        }
```

必要なusing: `Core.Inventory`, `Game.Block.Blocks.Machine.RecipeSelection`。

4. `GetBlockStateDetails` の `MachineBlockStateDetail.CreateState(processingRate, RecipeGuid)` を `CreateState(processingRate, RecipeGuid, SelectedRecipeGuid)` に変更。

- [ ] **Step 8: MachineBlockStateDetail に選択レシピを追加**

`Game.Block.Interface/State/MachineBlockStateDetail.cs`:

```csharp
        /// <summary>
        ///    選択中のレシピのGUID（未選択はGuid.Empty）
        /// </summary>
        [Key(2)] public string SelectedRecipeGuid;

        public MachineBlockStateDetail(float processingRate, Guid machineRecipeGuid, Guid selectedRecipeGuid)
        {
            ProcessingRate = processingRate;
            MachineRecipeGuid = machineRecipeGuid.ToString();
            SelectedRecipeGuid = selectedRecipeGuid.ToString();
        }

        public static BlockStateDetail CreateState(float processingRate, Guid machineRecipeGuid, Guid selectedRecipeGuid)
        {
            var stateDetail = new MachineBlockStateDetail(processingRate, machineRecipeGuid, selectedRecipeGuid);
            return new BlockStateDetail(BlockStateDetailKey, MessagePackSerializer.Serialize(stateDetail));
        }
```

旧2引数コンストラクタ/CreateStateは削除し、呼び出し側（`CleanRoomMachineProcessorComponent.cs:75` 含む）をコンパイルエラー駆動で全て更新する（CleanRoomは暫定で `Guid.Empty` を渡し、Task 3で正式対応）。

- [ ] **Step 9: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionTest"`
Expected: 全PASS

- [ ] **Step 10: コミット**

```bash
git add -A && git commit -m "feat(machine): レシピ選択必須化とIdle判定の置き換え・選択変更の返却フロー"
```

---

### Task 3: 加工中変更の返却テストと CleanRoom 機械対応

**Files:**
- Create: `Tests/CombinedTest/Core/MachineRecipeChangeRefundTest.cs`
- Create: `Game.Block/Blocks/CleanRoom/Machine/CleanRoomChipDrawApplyUtil.cs`
- Modify: `Game.Block/Blocks/CleanRoom/Machine/CleanRoomMachineProcessorComponent.cs`

**Interfaces:**
- Consumes: Task 2の `IMachineRecipeSelectorComponent` / `MachineRecipeSelectionUtil` / `ProcessingMachineProcessState.CurrentRecipe`
- Produces: `CleanRoomMachineProcessorComponent` が `IMachineRecipeSelectorComponent` を実装（メンバはTask 2のVanilla実装と同一シグネチャ）

- [ ] **Step 1: 返却フローの失敗テストを書く**

`Tests/CombinedTest/Core/MachineRecipeChangeRefundTest.cs` を新規作成。テストケース（初期化・設置・選択のパターンは `MachineRecipeSelectionTest` と同じ。加工開始は「選択→材料投入→`GameUpdater.UpdateWithWait()` 1回→Processing確認、ただし完了前」の状態を作る。レシピ時間が短い場合は `recipe.Time` が大きいレシピを選ぶか、tickを進めない）:

```csharp
[Test] public void RefundToInputInventoryTest()
// 加工中に別レシピをSet → Success。入力スロットに消費済み材料が戻り、CurrentStateがIdle、
// SelectedRecipeGuidが新レシピ、出力スロットは空（pendingOutputs破棄）であること

[Test] public void RefundOverflowGoesToPlayerInventoryTest()
// 入力スロットを他アイテムで満杯にしてから加工中変更 → Success。
// 戻り材料がoverflow（テストではOpenableInventoryItemDataStoreService）に入ること

[Test] public void RefundImpossibleCancelsChangeTest()
// 入力スロット満杯 + overflowをスロット0個(new OpenableInventoryItemDataStoreService((_,_)=>{}, factory, 0))にして
// 加工中変更 → RefundFailed。CurrentStateはProcessingのまま、SelectedRecipeGuidは旧レシピのまま、
// 入力スロット内容が変化していないこと（部分返却なし）

[Test] public void ClearDuringProcessingRefundsTest()
// 加工中にClearSelectedRecipe → Success。材料返却・Idle・SelectedRecipeGuid == Guid.Empty

[Test] public void SameRecipeReSelectIsNoOpTest()
// 加工中に同一レシピをSet → Success。CurrentStateはProcessingのまま、RemainingTicks等が変化しないこと

[Test] public void IsRemainInputIsNotRefundedTest()
// isRemain=trueの入力を持つレシピ（テストモッドに存在しなければTask 1と同様にJSON追加）で加工中変更。
// isRemainアイテムは加工開始時に消費されていない（スロットに残っている）ため、返却で二重にならないこと
// （変更後の入力スロットの当該アイテム数 == 投入数のままであることを確認）

[Test] public void FluidRefundBestEffortTest()
// 入力液体を持つレシピ（MachineFluidIOTest.cs参照）で加工開始→タンクへ別途液体を満杯まで注入→レシピ変更 → Success。
// 変更は成立し、タンク容量を超えた分の液体は消えていること（Amount <= Capacity）
```

各テストは上記コメントの検証を `Assert` で明示的に書くこと。「加工中」状態の作り方・電力供給は `GearMachineIoTest.cs` / `MachineFluidIOTest.cs` の既存パターンを流用する。

- [ ] **Step 2: 実行して失敗するもの・通るものを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeChangeRefundTest"`
Expected: Task 2実装が正しければ大半PASS。FAILがあればTask 2実装のバグとして修正（テストを実装に合わせて曲げない）

- [ ] **Step 3: CleanRoomMachineProcessorComponent に選択機能を実装**

1. `Update()` 内のローカル関数 `ApplyChipDrawOnCompletion`/`DrawSlot` を `CleanRoomChipDrawApplyUtil.cs`（`internal static class`、メソッド `public static void ApplyChipDrawOnCompletion(ProcessingMachineProcessState processingState, CleanRoomEffect cleanRoomEffect, BlockInstanceId blockInstanceId, ref uint cycleCount)`）へ移動する。ロジックは現行コードの移植のみで変更しない。
2. `CleanRoomMachineProcessorComponent` に `IMachineRecipeSelectorComponent` を実装。実装コードはTask 2のVanilla版と同一（`SelectedRecipeGuid` / `SetSelectedRecipe` / `ClearSelectedRecipe` / `ChangeSelection`）。ただし `ChangeSelection` 内の状態遷移は以下（Halted中に凍結ジョブがある場合も返却対象になるよう、CurrentState判定でなくジョブ有無で動くのはUtil側で担保済み）:

```csharp
            if (CurrentState != ProcessState.Idle) CurrentState = ProcessState.Idle;
```

（次のUpdateで清浄室条件が満たされなければ既存ロジックが再びHaltedへ倒す）
3. `GetBlockStateDetails` の `MachineBlockStateDetail.CreateState(...)` 第3引数を `SelectedRecipeGuid` に差し替え（Task 2 Step 8の暫定 `Guid.Empty` を解消）。

- [ ] **Step 4: CleanRoomのテスト確認**

`CleanRoomMachineTest` / `CleanRoomChipOutputTest` はこの時点では未修正のためFAILしてよい（Task 5で一括修正）。コンパイルのみ確認:

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [ ] **Step 5: コミット**

```bash
git add -A && git commit -m "feat(machine): 加工中レシピ変更の返却テストとCleanRoom機械のレシピ選択対応"
```

---

### Task 4: セーブ/ロードと Blueprint 対応

**Files:**
- Modify: `Game.Block/Blocks/Machine/VanillaMachineProcessorSaveJsonObject.cs`（selectedRecipeGuid追加）
- Modify: `Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs`（保存・ロードctor）
- Modify: `Game.Block/Factory/BlockTemplate/BlockTemplateUtil.cs`（MachineLoadStateで復元）
- Modify: `Game.Block/Blocks/CleanRoom/Machine/CleanRoomMachineProcessorSaveState.cs`
- Modify: `Game.Block/Blocks/CleanRoom/Machine/CleanRoomMachineProcessorComponent.cs`（復元受け取り）
- Create: `Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeBlueprintSettingsComponent.cs`
- Modify: `Game.Block/Factory/BlockTemplate/VanillaMachineTemplate.cs` / `VanillaGearMachineTemplate.cs` / CleanRoom機械Template（`grep -rn "CleanRoomMachineProcessorComponent" moorestech_server/Assets/Scripts/Game.Block/Factory` で特定）
- Test: `Tests/UnitTest/Game/SaveLoad/MachineRecipeSelectionSaveLoadTest.cs`（新規）

**Interfaces:**
- Consumes: Task 2/3の選択API
- Produces: セーブJSONの `processor.selectedRecipeGuid`（string、未選択はキー省略またはnull）。Blueprint設定キー `"MachineRecipeSelection"`、JSON形式 `{"selectedRecipeGuid":"<guid>"}`

- [ ] **Step 1: 失敗するセーブ/ロードテストを書く**

`Tests/UnitTest/Game/SaveLoad/MachineRecipeSelectionSaveLoadTest.cs`。既存 `MachineSaveLoadTest.cs` のセーブ→ロードのボイラープレートを流用して:

```csharp
[Test] public void SelectedRecipeIsRestoredTest()
// 機械設置→レシピ選択（加工はさせない）→セーブ→ロード→
// IMachineRecipeSelectorComponent.SelectedRecipeGuid が復元されていること

[Test] public void MissingSelectedRecipeGuidFallsBackToUnselectedTest()
// セーブJSON文字列内のselectedRecipeGuidを存在しないGUIDに書き換えてロード→
// SelectedRecipeGuid == Guid.Empty（未選択フォールバック）で例外が出ないこと
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionSaveLoadTest"`
Expected: FAIL（selectedRecipeGuidが保存されない）

- [ ] **Step 3: セーブ/ロードを実装**

1. `VanillaMachineProcessorSaveJsonObject` に追加:

```csharp
        // 選択中レシピ。未選択はnull（旧セーブもキー無し=null）
        // Selected recipe; null when unselected (older saves lack the key)
        [JsonProperty("selectedRecipeGuid")]
        public string SelectedRecipeGuidStr;
```

2. `VanillaMachineProcessorComponent.GetSaveJsonObject()` に `SelectedRecipeGuidStr = _context.SelectedRecipe?.MachineRecipeGuid.ToString(),` を追加。
3. セーブ復元用コンストラクタに `MachineRecipeMasterElement selectedRecipe` 引数を追加し、private ctor 内で `_context.SelectedRecipe = selectedRecipe;`（新規作成ctorは null を渡す）。呼び出し側はコンパイルエラー駆動で更新。
4. `BlockTemplateUtil.MachineLoadState` に復元処理を追加:

```csharp
            // 選択レシピを復元。GUIDがマスタから消えていれば未選択に戻す（機械は停止するが壊れない）
            // Restore the selected recipe; a GUID missing from the master falls back to unselected
            MachineRecipeMasterElement selectedRecipe = null;
            if (!string.IsNullOrEmpty(processorJson.SelectedRecipeGuidStr) && Guid.TryParse(processorJson.SelectedRecipeGuidStr, out var selectedGuid))
            {
                selectedRecipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(selectedGuid);
            }
```

（processorコンストラクタへ渡す）。加工中レシピの復元 `GetRecipeElement(processorJson.RecipeGuid)` はTask 1で辞書引き（missing→null）になったので既存のnullフォールバック（`VanillaMachineProcessorComponent` ctorのIdle戻し）がそのまま機能することを確認する。
5. CleanRoom: `CleanRoomMachineProcessorSaveJsonObject` に同フィールド追加、`Build` に `MachineRecipeMasterElement selectedRecipe` 引数追加、`Restore` に `out MachineRecipeMasterElement selectedRecipe` 追加、コンポーネントのctor/GetSaveStateを対応させる。

- [ ] **Step 4: Blueprint コンポーネントを実装**

`Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeBlueprintSettingsComponent.cs`:

```csharp
using System;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Context;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Machine.RecipeSelection
{
    /// <summary>
    ///     機械のレシピ選択をBlueprintでコピーするための設定コンポーネント（Vanilla/CleanRoom共用）
    ///     Blueprint settings component copying the machine recipe selection (shared by vanilla and clean-room)
    /// </summary>
    public class MachineRecipeBlueprintSettingsComponent : IBlockBlueprintSettings
    {
        public const string SettingsKey = "MachineRecipeSelection";
        public string BlueprintSettingsKey => SettingsKey;

        private readonly IMachineRecipeSelectorComponent _selector;

        public MachineRecipeBlueprintSettingsComponent(IMachineRecipeSelectorComponent selector)
        {
            _selector = selector;
        }

        public string GetBlueprintSettingsJson()
        {
            var guid = _selector.SelectedRecipeGuid;
            return JsonConvert.SerializeObject(new SettingsJsonObject
            {
                SelectedRecipeGuid = guid == Guid.Empty ? null : guid.ToString(),
            });
        }

        public void ApplyBlueprintSettingsJson(string json)
        {
            var settings = JsonConvert.DeserializeObject<SettingsJsonObject>(json);
            if (settings?.SelectedRecipeGuid == null) return;
            if (!Guid.TryParse(settings.SelectedRecipeGuid, out var guid)) return;

            // 別ブロックのレシピ・未アンロックはSetSelectedRecipe内の検証で弾かれ、未選択のまま設置される
            // Recipes of another block or locked ones are rejected by SetSelectedRecipe and the block stays unselected
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(guid);
            if (recipe == null) return;

            // 適用は設置直後（必ずIdle・ジョブ無し）のため返却は発生しない。溢れ先はダミーで良い
            // Applied right after creation (always idle, no job), so no refund occurs; a dummy overflow suffices
            var dummyOverflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 0);
            _selector.SetSelectedRecipe(recipe, dummyOverflow);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }

        private class SettingsJsonObject
        {
            [JsonProperty("selectedRecipeGuid")] public string SelectedRecipeGuid;
        }
    }
}
```

Template 3箇所（VanillaMachineTemplate の New/Load、VanillaGearMachineTemplate の同等箇所、CleanRoom機械Template）で `components` リストに `new MachineRecipeBlueprintSettingsComponent(processor)` を追加する。

- [ ] **Step 5: Blueprintテストを書く・実行**

既存 `Tests/CombinedTest/Game/BlueprintFilterSplitterSettingsTest.cs` のパターンを流用し、`Tests/CombinedTest/Game/BlueprintMachineRecipeSelectionTest.cs` を新規作成: 機械設置→レシピ選択→Blueprint作成→別位置に適用→適用先ブロックの `SelectedRecipeGuid` が一致すること。＋別ブロックGUIDを含むBlueprint JSONを直接 `ApplyBlueprintSettingsJson` に渡して未選択のままであること。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionSaveLoadTest|BlueprintMachineRecipeSelectionTest"`
Expected: 全PASS

- [ ] **Step 6: コミット**

```bash
git add -A && git commit -m "feat(machine): レシピ選択のセーブ/ロードとBlueprint対応"
```

---

### Task 5: 既存テストの一斉移行

**Files:**
- Create: `Tests/Util/MachineRecipeSelectTestUtil.cs`
- Modify: 自動判定前提の既存テスト（下記リスト起点に全件）

**Interfaces:**
- Consumes: Task 2の `IMachineRecipeSelectorComponent`
- Produces: `MachineRecipeSelectTestUtil.SelectRecipe(IBlock machineBlock, MachineRecipeMasterElement recipe)`（後続タスクのテストでも使用可）

- [ ] **Step 1: テストヘルパーを作成**

```csharp
using Core.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;

namespace Tests.Util
{
    // 自動判定前提だった既存テストを最小変更で移行するためのレシピ選択ヘルパー
    // Helper to migrate auto-detection-era tests to explicit recipe selection with minimal edits
    public static class MachineRecipeSelectTestUtil
    {
        public static void SelectRecipe(IBlock machineBlock, MachineRecipeMasterElement recipe)
        {
            Assert.IsTrue(machineBlock.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector), "機械ブロックではありません");
            var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 100);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(recipe, overflow), "テスト用レシピ選択に失敗");
        }
    }
}
```

- [ ] **Step 2: 全テストを実行して失敗一覧を取得**

Run: `uloop run-tests --project-path ./moorestech_client`（フィルタ無し全件。時間がかかる）
Expected: 自動加工前提のテストがFAIL。失敗一覧を記録する

- [ ] **Step 3: 失敗テストを1ファイルずつ移行**

修正パターン: 機械ブロック設置後・アイテム投入前に `MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);` を1行追加する（テストは既に `recipe` をマスタから取得している場合が大半。無い場合は `MasterHolder.MachineRecipesMaster.MachineRecipes.Data` から該当ブロック・入力のものを選ぶ）。

事前調査で判明している主な対象（これに限らず失敗一覧全件を直す）:
`GearMachineIoTest` / `MachineFluidIOTest` / `MachineModuleSlotTest` / `QualityModuleOutputTest` / `IdlePowerRateTest` / `GearIdleDemandRecalcTest` / `MachineMultiSegmentPowerSupplyTest` / `CleanRoomMachineTest` / `CleanRoomChipOutputTest` / `CleanRoomSaveLoadTest` / `MachineSaveLoadTest` / `FluidMachineSaveLoadTest` / `GearMachineSaveLoadTest` / `BlockInventoryUpdateEventPacketTest` / `InvokeBlockStateEventProtocolTest` / `RequestBlockInventoryTest` / `ChallengeCompletedEventTest` ほか。

注意:
- セーブ/ロード系は「選択→セーブ→ロード後も加工が継続する」ことの検証になるため、ロード後の再選択は**行わない**（復元がTask 4で実装済み）。
- 「加工されないこと」を検証しているテスト（存在すれば）は選択を追加しない。
- テストの期待値自体を変えるのは「自動判定で勝手に加工が始まる」ことを前提にしたassertのみ。

- [ ] **Step 4: 全テストを再実行**

Run: `uloop run-tests --project-path ./moorestech_client`
Expected: 全PASS（クライアント側EditModeInPlayingTestの機械系がFAILする場合はTask 7-8のUI変更が必要なものか判別し、UI起因ならTask 8まで保留メモを残す）

- [ ] **Step 5: コミット**

```bash
git add -A && git commit -m "test: 既存テストをレシピ明示選択へ一斉移行"
```

---

### Task 6: プロトコル MachineRecipeSelectionProtocol

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MachineRecipeSelectionProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`
- Test: `Tests/CombinedTest/Server/PacketTest/MachineRecipeSelectionProtocolTest.cs`

**Interfaces:**
- Consumes: `IMachineRecipeSelectorComponent`（Task 2）、`IPlayerInventoryDataStore`、`MachineRecipesMaster.GetRecipeElement(Guid)`
- Produces:
  - タグ `va:machineRecipeSelection`、`MachineRecipeSelectionRequest.CreateSetRecipeRequest(Vector3Int position, Guid machineRecipeGuid, int playerId)` / `CreateClearRequest(Vector3Int position, int playerId)`
  - `MachineRecipeSelectionResponse { bool Success; MachineRecipeSelectionFailureReason FailureReason; string SelectedRecipeGuid; }`
  - クライアント: `VanillaApi.Response.SendMachineRecipeSelectionRequest(request, ct)`

- [ ] **Step 1: 失敗するパケットテストを書く**

`FilterSplitterStateProtocolTest.cs` の `Send` ヘルパーパターンを流用して作成:

```csharp
[Test] public void SetRecipeSelectsAndRespondsTest()
// 機械設置→CreateSetRecipeRequest送信→Success、SelectedRecipeGuidが一致、
// コンポーネントのSelectedRecipeGuidも一致

[Test] public void ClearResetsSelectionTest()
// 選択後にCreateClearRequest→Success、SelectedRecipeGuid == Guid.Empty.ToString()

[Test] public void WrongBlockRecipeIsRejectedTest()
// 機械Aに機械B用レシピGUID→Success=false, FailureReason.RecipeBlockMismatch

[Test] public void LockedRecipeIsRejectedTest()
// initialUnlocked:falseレシピ→FailureReason.RecipeLocked

[Test] public void UnknownRecipeGuidIsRejectedTest()
// でたらめGUID→FailureReason.InvalidRecipe

[Test] public void NotMachineBlockIsRejectedTest()
// Chest設置位置に送信→FailureReason.NotMachine

[Test] public void BlockNotFoundTest()
// 何も無い座標→FailureReason.BlockNotFound

[Test] public void RefundFailedIsReportedTest()
// 加工中＋入力満杯＋プレイヤーインベントリ満杯でSetRecipe→FailureReason.RefundFailed
// （プレイヤーインベントリはserviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId)で満杯化）
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionProtocolTest"`
Expected: コンパイルエラー（プロトコル未定義）

- [ ] **Step 3: プロトコルを実装**

`Server.Protocol/PacketResponse/MachineRecipeSelectionProtocol.cs`（creating-server-protocolスキル準拠。enumはintに変換しない・Operationごとのstatic factory・`[Obsolete]`引数なしctor必須）:

```csharp
using System;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// 機械のレシピ選択を設定・解除するプロトコル。Operation により SetRecipe / Clear を切り替える。
    /// 選択状態の取得は既存のブロック状態同期（MachineBlockStateDetail）で行うため Get は持たない。
    /// Protocol to set/clear the machine recipe selection; reads go through the existing block state sync.
    /// </summary>
    public class MachineRecipeSelectionProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:machineRecipeSelection";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public MachineRecipeSelectionProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<MachineRecipeSelectionRequest>(payload);
            if (request.Position == null) return Fail(MachineRecipeSelectionFailureReason.InvalidRequest);

            var block = ServerContext.WorldBlockDatastore.GetBlock(request.Position.Vector3Int);
            if (block == null) return Fail(MachineRecipeSelectionFailureReason.BlockNotFound);
            if (!block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector))
            {
                return Fail(MachineRecipeSelectionFailureReason.NotMachine);
            }

            // 加工中変更の返却先としてリクエスト元プレイヤーのメインインベントリを渡す
            // Pass the requesting player's main inventory as the refund overflow target
            var playerInventory = _playerInventoryDataStore.GetInventoryData(request.PlayerId).MainOpenableInventory;

            switch (request.Operation)
            {
                case MachineRecipeSelectionOperation.SetRecipe:
                    if (!Guid.TryParse(request.MachineRecipeGuidStr, out var recipeGuid)) return Fail(MachineRecipeSelectionFailureReason.InvalidRecipe);
                    var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid);
                    if (recipe == null) return Fail(MachineRecipeSelectionFailureReason.InvalidRecipe);

                    var setResult = selector.SetSelectedRecipe(recipe, playerInventory);
                    if (setResult != MachineRecipeSelectionResult.Success) return Fail(ToFailureReason(setResult));
                    break;
                case MachineRecipeSelectionOperation.Clear:
                    var clearResult = selector.ClearSelectedRecipe(playerInventory);
                    if (clearResult != MachineRecipeSelectionResult.Success) return Fail(ToFailureReason(clearResult));
                    break;
                default:
                    return Fail(MachineRecipeSelectionFailureReason.UnknownOperation);
            }

            return new MachineRecipeSelectionResponse(true, MachineRecipeSelectionFailureReason.None, selector.SelectedRecipeGuid.ToString());

            #region Internal

            static MachineRecipeSelectionResponse Fail(MachineRecipeSelectionFailureReason reason)
            {
                return new MachineRecipeSelectionResponse(false, reason, Guid.Empty.ToString());
            }

            static MachineRecipeSelectionFailureReason ToFailureReason(MachineRecipeSelectionResult result)
            {
                return result switch
                {
                    MachineRecipeSelectionResult.RecipeBlockMismatch => MachineRecipeSelectionFailureReason.RecipeBlockMismatch,
                    MachineRecipeSelectionResult.RecipeLocked => MachineRecipeSelectionFailureReason.RecipeLocked,
                    MachineRecipeSelectionResult.RefundFailed => MachineRecipeSelectionFailureReason.RefundFailed,
                    _ => MachineRecipeSelectionFailureReason.UnknownOperation,
                };
            }

            #endregion
        }

        #region MessagePack

        [MessagePackObject]
        public class MachineRecipeSelectionRequest : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Position { get; set; }
            [Key(3)] public MachineRecipeSelectionOperation Operation { get; set; }
            [Key(4)] public string MachineRecipeGuidStr { get; set; }
            [Key(5)] public int PlayerId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public MachineRecipeSelectionRequest() { Tag = ProtocolTag; }

            // Operation ごとに必要なフィールドだけを設定する private コンストラクタ
            // Private constructor; static factories below set only the fields each Operation needs
            private MachineRecipeSelectionRequest(Vector3Int position, MachineRecipeSelectionOperation operation, string machineRecipeGuidStr, int playerId)
            {
                Tag = ProtocolTag;
                Position = new Vector3IntMessagePack(position);
                Operation = operation;
                MachineRecipeGuidStr = machineRecipeGuidStr;
                PlayerId = playerId;
            }

            public static MachineRecipeSelectionRequest CreateSetRecipeRequest(Vector3Int position, Guid machineRecipeGuid, int playerId)
            {
                return new MachineRecipeSelectionRequest(position, MachineRecipeSelectionOperation.SetRecipe, machineRecipeGuid.ToString(), playerId);
            }

            public static MachineRecipeSelectionRequest CreateClearRequest(Vector3Int position, int playerId)
            {
                return new MachineRecipeSelectionRequest(position, MachineRecipeSelectionOperation.Clear, Guid.Empty.ToString(), playerId);
            }
        }

        [MessagePackObject]
        public class MachineRecipeSelectionResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public MachineRecipeSelectionFailureReason FailureReason { get; set; }
            [Key(4)] public string SelectedRecipeGuid { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public MachineRecipeSelectionResponse() { }

            public MachineRecipeSelectionResponse(bool success, MachineRecipeSelectionFailureReason failureReason, string selectedRecipeGuid)
            {
                Tag = ProtocolTag;
                Success = success;
                FailureReason = failureReason;
                SelectedRecipeGuid = selectedRecipeGuid;
            }
        }

        public enum MachineRecipeSelectionOperation
        {
            SetRecipe = 0,
            Clear = 1,
        }

        public enum MachineRecipeSelectionFailureReason
        {
            None = 0,
            BlockNotFound = 1,
            NotMachine = 2,
            InvalidRecipe = 3,
            RecipeBlockMismatch = 4,
            RecipeLocked = 5,
            RefundFailed = 6,
            UnknownOperation = 7,
            InvalidRequest = 8,
        }

        #endregion
    }
}
```

`PacketResponseCreator.cs` のコンストラクタに登録（FilterSplitterの行の近く）:

```csharp
            _packetResponseDictionary.Add(MachineRecipeSelectionProtocol.ProtocolTag, new MachineRecipeSelectionProtocol(serviceProvider));
```

`VanillaApiWithResponse.cs` にメソッド1本追加（1プロトコル=1メソッド。Requestは呼び出し側で構築）:

```csharp
        public async UniTask<MachineRecipeSelectionProtocol.MachineRecipeSelectionResponse> SendMachineRecipeSelectionRequest(MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest request, CancellationToken ct)
        {
            return await _packetExchangeManager.GetPacketResponse<MachineRecipeSelectionProtocol.MachineRecipeSelectionResponse>(request, ct);
        }
```

- [ ] **Step 4: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionProtocolTest"`
Expected: 全PASS

- [ ] **Step 5: コミット**

```bash
git add -A && git commit -m "feat(protocol): va:machineRecipeSelection プロトコル追加"
```

---

### Task 7: クライアント — レシピ選択パネル

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/MachineRecipeSelectionPanel.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/MachineBlockInventoryView.cs`

**Interfaces:**
- Consumes: `SendMachineRecipeSelectionRequest`（Task 6）、`MachineBlockStateDetail.SelectedRecipeGuid`（Task 2）、`IGameUnlockStateData`（DI済み: `MainGameStarter.cs:252`）、`ClientContext.PlayerConnectionSetting.PlayerId`、`ItemSlotView.Prefab` / `OnLeftClickUp` / `OnRightClickUp` / `SetSlotViewOption`
- Produces: `MachineRecipeSelectionPanel.Initialize(BlockGameObject blockGameObject)`（Task 8がプレハブに配置・配線）

- [ ] **Step 1: パネルを実装**

`MachineRecipeSelectionPanel.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.State;
using Game.UnlockState;
using Mooresmaster.Model.MachineRecipesModule;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.Inventory.Block
{
    /// <summary>
    /// 機械のレシピ選択パネル。対象ブロックのアンロック済みレシピを一覧表示し、クリックで選択する。
    /// 選択状態はブロック状態同期（MachineBlockStateDetail.SelectedRecipeGuid）から毎フレーム反映する。
    /// Recipe selection panel; lists unlocked recipes of the block and applies selection via protocol.
    /// </summary>
    public class MachineRecipeSelectionPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform recipeSlotParent;

        [Inject] private IGameUnlockStateData _gameUnlockStateData;

        private readonly List<(ItemSlotView view, MachineRecipeMasterElement recipe)> _slots = new();
        private readonly CompositeDisposable _subscriptions = new();
        private BlockGameObject _blockGameObject;
        private CancellationTokenSource _cts;
        private string _lastSelectedGuidStr;

        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            _cts = new CancellationTokenSource();
            BuildRecipeSlots();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _subscriptions.Dispose();
            _cts?.Dispose();
        }

        private void BuildRecipeSlots()
        {
            // 対象ブロックのアンロック済みレシピを共有マスタから導出する（一覧取得プロトコルは無い）
            // Derive the block's unlocked recipes from the shared master (no list protocol exists)
            var blockGuid = _blockGameObject.BlockMasterElement.BlockGuid;
            var unlockInfos = _gameUnlockStateData.MachineRecipeUnlockStateInfos;
            foreach (var recipe in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                if (recipe.BlockGuid != blockGuid) continue;
                if (!unlockInfos.TryGetValue(recipe.MachineRecipeGuid, out var info) || !info.IsUnlocked) continue;

                var slotView = Instantiate(ItemSlotView.Prefab, recipeSlotParent);
                SetRecipeView(slotView, recipe);

                var captured = recipe;
                slotView.OnLeftClickUp.Subscribe(_ => SendSetRecipe(captured).Forget()).AddTo(_subscriptions);
                slotView.OnRightClickUp.Subscribe(_ => SendClearIfSelected(captured).Forget()).AddTo(_subscriptions);
                _slots.Add((slotView, recipe));
            }

            #region Internal

            void SetRecipeView(ItemSlotView slotView, MachineRecipeMasterElement recipe)
            {
                // 先頭出力アイテムをアイコンとして表示し、ツールチップに入出力を並べる
                // Show the first output item as the icon; list inputs and outputs in the tooltip
                var outputItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
                var itemView = ClientContext.ItemImageContainer.GetItemView(outputItemId);
                slotView.SetItem(itemView, recipe.OutputItems[0].Count, BuildToolTip(recipe));
            }

            string BuildToolTip(MachineRecipeMasterElement recipe)
            {
                var inputs = new List<string>();
                foreach (var input in recipe.InputItems) inputs.Add($"{MasterHolder.ItemMaster.GetItemMaster(input.ItemGuid).Name}×{input.Count}");
                var outputs = new List<string>();
                foreach (var output in recipe.OutputItems) outputs.Add($"{MasterHolder.ItemMaster.GetItemMaster(output.ItemGuid).Name}×{output.Count}");
                return $"{string.Join(" + ", inputs)} → {string.Join(" + ", outputs)} ({recipe.Time}秒)";
            }

            #endregion
        }

        private void Update()
        {
            // 選択状態はサーバーのブロック状態同期から導出する（Set応答を待たずとも変化が反映される）
            // Selection highlight derives from the synced block state, so external changes also show up
            var state = _blockGameObject.GetStateDetail<MachineBlockStateDetail>(MachineBlockStateDetail.BlockStateDetailKey);
            if (state == null || state.SelectedRecipeGuid == _lastSelectedGuidStr) return;
            _lastSelectedGuidStr = state.SelectedRecipeGuid;

            foreach (var (view, recipe) in _slots)
            {
                var isSelected = recipe.MachineRecipeGuid.ToString() == state.SelectedRecipeGuid;
                view.SetSlotViewOption(new CommonSlotViewOption { HotBarSelected = isSelected });
            }
        }

        private async UniTaskVoid SendSetRecipe(MachineRecipeMasterElement recipe)
        {
            var cts = _cts;
            if (cts == null) return;
            var request = MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRecipeRequest(
                _blockGameObject.BlockPosInfo.OriginalPos, recipe.MachineRecipeGuid, ClientContext.PlayerConnectionSetting.PlayerId);
            var response = await ClientContext.VanillaApi.Response.SendMachineRecipeSelectionRequest(request, cts.Token);
            if (cts.IsCancellationRequested) return;
            if (response == null || !response.Success) Debug.Log($"レシピ選択失敗: {response?.FailureReason}");
        }

        private async UniTaskVoid SendClearIfSelected(MachineRecipeMasterElement recipe)
        {
            // 選択中のレシピを右クリックした時だけ解除する
            // Right-click clears only when the clicked recipe is the selected one
            if (recipe.MachineRecipeGuid.ToString() != _lastSelectedGuidStr) return;
            var cts = _cts;
            if (cts == null) return;
            var request = MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateClearRequest(
                _blockGameObject.BlockPosInfo.OriginalPos, ClientContext.PlayerConnectionSetting.PlayerId);
            var response = await ClientContext.VanillaApi.Response.SendMachineRecipeSelectionRequest(request, cts.Token);
            if (cts.IsCancellationRequested) return;
            if (response == null || !response.Success) Debug.Log($"レシピ選択解除失敗: {response?.FailureReason}");
        }
    }
}
```

注意: `CommonSlotViewOption` のフィールド名（`HotBarSelected`）は `CommonSlotView.cs:98` を実装時に確認。`ClientContext.ItemImageContainer.GetItemView` のAPI名も既存View（`ItemListView.cs` 等）を確認して合わせる。うまく合わない場合はツールチップ・アイコン取得だけ既存前例に置き換え、構造（Instantiate＋Subscribe＋状態導出Update）は維持する。

- [ ] **Step 2: MachineBlockInventoryView にパネル接続と未設定表示を追加**

```csharp
        [SerializeField] private MachineRecipeSelectionPanel recipeSelectionPanel;
```

`Initialize` の末尾（`#region Internal` の上）に:

```csharp
            recipeSelectionPanel.Initialize(blockGameObject);
```

`UpdateMachineRecipeView` の産出レート表示に未設定分岐を追加（`machineRecipeCountText` 構築部分の前）:

```csharp
                // 未選択の機械は加工しないため、明示的にその旨を表示する
                // An unselected machine never processes, so state it explicitly
                if (state.SelectedRecipeGuid == Guid.Empty.ToString())
                {
                    machineRecipeCount.text = "レシピ未設定";
                    return;
                }
```

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（プレハブ未配線でも `recipeSelectionPanel` がnullになるのは次タスクで解消。nullチェックは追加しない — プレハブ配置が設計上の保証）

- [ ] **Step 4: コミット**

```bash
git add -A && git commit -m "feat(client): 機械UIのレシピ選択パネルと未設定表示"
```

---

### Task 8: UIプレハブ配線（uloop経由）と実機確認

**Files:**
- Modify（uloop execute-dynamic-code経由のみ）: `moorestech_client/Assets/AddressableResources/UI/Block/MachineBlockInventory.prefab` / `GearMachineBlockInventory.prefab`

**Interfaces:**
- Consumes: `MachineRecipeSelectionPanel`（Task 7）
- Produces: 両プレハブに `RecipeSelectionPanel` 子オブジェクト（`MachineRecipeSelectionPanel` + `recipeSlotParent` 用 `GridLayoutGroup` コンテナ）が配置され、`MachineBlockInventoryView.recipeSelectionPanel` が配線済み

- [ ] **Step 1: プレハブ構造を確認**

`uloop execute-dynamic-code` で `MachineBlockInventory.prefab` をロードし、ルート直下の子（レイアウト構造・既存の `machineRecipeCount` テキストの位置）をログ出力して、パネルの挿入先（機械名テキストや進捗矢印と同じ親のUI領域）を決める。

- [ ] **Step 2: uloopでプレハブに要素を追加・配線**

`uloop execute-dynamic-code` で以下を両プレハブに対して実行する（コードは1プレハブ分。パスを変えて2回実行）:

```csharp
// PrefabUtility.LoadPrefabContents → 編集 → SaveAsPrefabAsset の標準手順
// Standard flow: LoadPrefabContents, edit, then SaveAsPrefabAsset
var path = "Assets/AddressableResources/UI/Block/MachineBlockInventory.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);

// パネル本体（MachineRecipeSelectionPanel）とスロット親（GridLayoutGroup）を作成
var panelGo = new UnityEngine.GameObject("RecipeSelectionPanel", typeof(UnityEngine.RectTransform));
panelGo.transform.SetParent(root.transform, false);
var panel = panelGo.AddComponent<Client.Game.InGame.UI.Inventory.Block.MachineRecipeSelectionPanel>();

var slotParentGo = new UnityEngine.GameObject("RecipeSlots", typeof(UnityEngine.RectTransform));
slotParentGo.transform.SetParent(panelGo.transform, false);
var grid = slotParentGo.AddComponent<UnityEngine.UI.GridLayoutGroup>();
grid.cellSize = new UnityEngine.Vector2(80, 80);
grid.spacing = new UnityEngine.Vector2(4, 4);

// RectTransformの配置はStep 1で確認した挿入先に合わせて設定する
// Position the RectTransform based on the layout inspected in Step 1

// SerializedObjectでSerializeFieldを配線
var panelSo = new UnityEditor.SerializedObject(panel);
panelSo.FindProperty("recipeSlotParent").objectReferenceValue = slotParentGo.GetComponent<UnityEngine.RectTransform>();
panelSo.ApplyModifiedProperties();

var view = root.GetComponent<Client.Game.InGame.UI.Inventory.Block.MachineBlockInventoryView>();
var viewSo = new UnityEditor.SerializedObject(view);
viewSo.FindProperty("recipeSelectionPanel").objectReferenceValue = panel;
viewSo.ApplyModifiedProperties();

UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
UnityEditor.PrefabUtility.UnloadPrefabContents(root);
```

GearMachineBlockInventory.prefab はルートコンポーネントが `GearMachineBlockInventoryView`（継承クラス）である点だけ異なる（`GetComponent<MachineBlockInventoryView>()` で基底型として取得可能）。

- [ ] **Step 3: EditModeInPlayingTestで表示検証**

`MachineModuleSlotUITest.cs` のパターン（PlayMode遷移→ブロック設置→UI Instantiate→子要素検証）で、`Client.Tests/EditModeInPlayingTest/MachineRecipeSelectionUITest.cs` を追加:
- UIを開いた時 `RecipeSlots` 配下に対象ブロックのアンロック済みレシピ数分の `ItemSlotView` が並ぶこと
- サーバー側でレシピを選択済みの機械では、該当スロットがハイライトされること（`CommonSlotViewOption` の反映を直接検証できない場合は `_lastSelectedGuidStr` 相当の表示状態をリフレクションか公開プロパティで確認）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionUITest"`
Expected: PASS（PlayMode遷移テスト後はドメインリロードエラーに注意。45秒待ってリトライ）

- [ ] **Step 4: スクリーンショットで視覚確認**

`uloop screenshot` でGame Viewを撮り、パネルがレイアウト崩れなく表示されていることを目視確認（uloop-screenshot / uloop-focus-window スキル参照）。崩れていればStep 2のRectTransform設定を調整。

- [ ] **Step 5: コミット**

```bash
git add -A && git commit -m "feat(client): 機械UIプレハブへレシピ選択パネルを配線"
```

（Unityが生成した.metaファイルもコミットに含める）

---

### Task 9: 全体検証

- [ ] **Step 1: フルコンパイルと全テスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0・警告の新規増加なし

Run: `uloop run-tests --project-path ./moorestech_client`
Expected: 全PASS

- [ ] **Step 2: 残存参照の掃除確認**

```bash
grep -rn "GetRecipeElementKey\|TryGetRecipeElement" moorestech_server moorestech_client --include="*.cs"
```
Expected: ヒット0（自動判定コードの完全撤去）

- [ ] **Step 3: specとの突合**

spec（`docs/superpowers/specs/2026-07-10-machine-recipe-selection-design.md`）の各要件（選択必須・搬入無制限・返却フロー・液体ベストエフォート・no-op・セーブ・Blueprint・UI・検証4項目）に対応する実装/テストがあることを確認し、乖離があれば修正またはspec更新。

- [ ] **Step 4: 最終コミット**

```bash
git add -A && git commit -m "feat: 機械レシピ任意選択（選択必須化）完成"
```

---

## Self-Review 済み事項

- **spec coverage**: 選択必須(T2)・自動判定削除(T1,T2)・返却フロー(T2,T3)・液体ベストエフォート(T2 Step6)・no-op(T2)・CleanRoom(T3)・セーブ/Blueprint(T4)・プロトコル(T6)・UI(T7,T8)・既存テスト(T5)・検証(T9)
- **specからの逸脱1件（意図的）**: プロトコルの `Get` Operationを廃止し、選択状態の同期を既存の `MachineBlockStateDetail`（ブロック状態同期チャネル）へのフィールド追加で行う。UIオープン時Get方式は同じ情報の第2経路になるため（SSOT）。specも修正済み
- **型整合**: `IMachineRecipeSelectorComponent` のシグネチャはT2定義とT3/T4/T5/T6の使用箇所で一致。`MachineBlockStateDetail.CreateState(float, Guid, Guid)` はT2で全呼び出し側更新
- **実装時要確認ポイント（コードが既存APIと僅かに違う可能性）**: `CommonSlotViewOption` のフィールド名、`ClientContext.ItemImageContainer.GetItemView`、CleanRoom機械Templateのファイル名、電気機械テストの電力供給方法 — いずれも参照すべき前例ファイルを本文に明記済み
