# GOLDEN: 人間が直した内容（捕捉できれば合格）
観点: SSOT(単一の出所) と メンバ配置(責務適合)

AIは機械レシピのアンロック判定を、レシピマスタ用ユーティリティ MachineRecipeMaster(MachineRecipeMasterUtil) に **static _cachedUnlockState** フィールド＋ ServerContext.GetService の遅延グローバル取得で実装した。

問題1(SSOT): アンロック状態の権威ソースは DI 提供の IGameUnlockStateData なのに、static キャッシュという**第2の出所**を別に持ち、両者が乖離しうる（保存/再生成で stale になる）。
問題2(配置/責務): アンロック判定ロジックを、レシピ参照を担う MachineRecipeMaster という**想定責務外のクラス**に static+グローバル取得で置いた（隠れ依存・責務漏れ）。アンロック判定は本来その状態を使う側のコンポーネントが DI で受け取って行うべき。

人間の実際の修正: static _cachedUnlockState とその ServerContext 直取得を削除し、アンロック判定を **VanillaMachineInputInventory に IGameUnlockStateData をコンストラクタ注入(DI)** して行う形へ移した（＝出所をDIの単一ソースに統合し、判定を責務を持つクラスへ配置）。

合格条件(SSOT reviewer): static キャッシュが DI 提供ソースと別の第2の出所になっている点を検知し、修正方針が『staticキャッシュを廃して単一ソース(DI)へ統合』であること。
合格条件(配置 reviewer): アンロック判定が MachineRecipeMaster という責務外クラスに置かれている点を検知し、修正方針が『判定を使う側コンポーネントへ移し DI で受け取る』であること。

```diff
commit e211e875955d444b2fa5c3d65ef9af7133bcf9f0
Author: sakastudio <sakastudio100@gmail.com>
Date:   Sun Jun 7 19:48:48 2026 +0900

    レビュー指摘修正: 機械レシピアンロック経路を補強

diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/CraftTree/TreeView/CraftTreeEditorNodeItem.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/CraftTree/TreeView/CraftTreeEditorNodeItem.cs
index 6cad88750..618969f29 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/CraftTree/TreeView/CraftTreeEditorNodeItem.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/CraftTree/TreeView/CraftTreeEditorNodeItem.cs
@@ -130,9 +130,12 @@ namespace Client.Game.InGame.CraftTree.TreeView
             List<CraftTreeNode> GetMaterialItems(RecipeViewerItemRecipes recipes)
             {
                 var materials = new List<CraftTreeNode>();
-                if (recipes.UnlockedCraftRecipes().Count == 0 && recipes.MachineRecipes.Count != 0)
+                var unlockedMachineRecipes = recipes.UnlockedMachineRecipes();
+                if (recipes.UnlockedCraftRecipes().Count == 0 && unlockedMachineRecipes.Count != 0)
                 {
-                    var machineRecipe = recipes.MachineRecipes.FirstOrDefault();
+                    // アンロック済み機械レシピだけをクラフトツリーに展開
+                    // Expand only unlocked machine recipes into the craft tree
+                    var machineRecipe = unlockedMachineRecipes.FirstOrDefault();
                     foreach (var inputItem in machineRecipe.Value.First().InputItems)
                     {
                         var itemId = MasterHolder.ItemMaster.GetItemId(inputItem.ItemGuid);
@@ -167,4 +170,4 @@ namespace Client.Game.InGame.CraftTree.TreeView
             #endregion
         }
     }
-}
\ No newline at end of file
+}
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/ItemRecipeViewerDataContainer.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/ItemRecipeViewerDataContainer.cs
index bcb8ded93..41bfae479 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/ItemRecipeViewerDataContainer.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/ItemRecipeViewerDataContainer.cs
@@ -128,7 +128,7 @@ namespace Client.Game.InGame.UI.Inventory.RecipeViewer
                 var unlockedRecipes = kv.Value
                     .Where(m => infos[m.MachineRecipeGuid].IsUnlocked)
                     .ToList();
-                if (unlockedRecipes.Count > 0)
+                if (0 < unlockedRecipes.Count)
                 {
                     result.Add(blockId, unlockedRecipes);
                 }
@@ -136,4 +136,4 @@ namespace Client.Game.InGame.UI.Inventory.RecipeViewer
             return result;
         }
     }
-}
\ No newline at end of file
+}
diff --git a/moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs b/moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs
index e24aa3736..003053da6 100644
--- a/moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs
+++ b/moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs
@@ -216,6 +216,21 @@ namespace Core.Master.Validator
                             }
                             break;
                         }
+                        case UnlockMachineRecipeGameActionParam unlockMachineRecipe:
+                        {
+                            if (unlockMachineRecipe.UnlockMachineRecipeGuids == null) break;
+                            foreach (var machineRecipeGuid in unlockMachineRecipe.UnlockMachineRecipeGuids)
+                            {
+                                // 機械レシピの参照先が存在することを検証
+                                // Validate that the referenced machine recipe exists
+                                var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(machineRecipeGuid);
+                                if (recipe == null)
+                                {
+                                    logs += $"[ChallengeMaster] Challenge:{challengeTitle} {actionType} has invalid UnlockMachineRecipeGuid:{machineRecipeGuid}\n";
+                                }
+                            }
+                            break;
+                        }
                         case GiveItemGameActionParam giveItem:
                         {
                             if (giveItem.RewardItems == null) break;
diff --git a/moorestech_server/Assets/Scripts/Core.Master/Validator/ResearchMasterUtil.cs b/moorestech_server/Assets/Scripts/Core.Master/Validator/ResearchMasterUtil.cs
index 2c62303b8..16a1cb5fd 100644
--- a/moorestech_server/Assets/Scripts/Core.Master/Validator/ResearchMasterUtil.cs
+++ b/moorestech_server/Assets/Scripts/Core.Master/Validator/ResearchMasterUtil.cs
@@ -105,6 +105,21 @@ namespace Core.Master.Validator
                             }
                             break;
                         }
+                        case UnlockMachineRecipeGameActionParam unlockMachineRecipe:
+                        {
+                            if (unlockMachineRecipe.UnlockMachineRecipeGuids == null) break;
+                            foreach (var machineRecipeGuid in unlockMachineRecipe.UnlockMachineRecipeGuids)
+                            {
+                                // 機械レシピの参照先が存在することを検証
+                                // Validate that the referenced machine recipe exists
+                                var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(machineRecipeGuid);
+                                if (recipe == null)
+                                {
+                                    localErrors += $"[ResearchMaster] Research:{researchName} has invalid ClearedAction.UnlockMachineRecipeGuid:{machineRecipeGuid}\n";
+                                }
+                            }
+                            break;
+                        }
                         case GiveItemGameActionParam giveItem:
                         {
                             if (giveItem.RewardItems == null) break;
diff --git a/moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineInputInventory.cs b/moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineInputInventory.cs
index 1c8683b06..4d9ecd3a3 100644
--- a/moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineInputInventory.cs
+++ b/moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineInputInventory.cs
@@ -7,6 +7,7 @@ using Game.Block.Interface;
 using Game.Block.Interface.Event;
 using Game.Context;
 using Game.Fluid;
+using Game.UnlockState;
 using Mooresmaster.Model.MachineRecipesModule;
 
 namespace Game.Block.Blocks.Machine.Inventory
@@ -25,13 +26,22 @@ namespace Game.Block.Blocks.Machine.Inventory
         
         private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
         private readonly FluidContainer[] _fluidContainers;
+        private readonly IGameUnlockStateData _gameUnlockStateData;
         private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
         
-        public VanillaMachineInputInventory(BlockId blockId, int inputSlot, int innerTankCount, float innerTankCapacity, BlockOpenableInventoryUpdateEvent blockInventoryUpdate, BlockInstanceId blockInstanceId)
+        public VanillaMachineInputInventory(
+            BlockId blockId,
+            int inputSlot,
+            int innerTankCount,
+            float innerTankCapacity,
+            BlockOpenableInventoryUpdateEvent blockInventoryUpdate,
+            BlockInstanceId blockInstanceId,
+            IGameUnlockStateData gameUnlockStateData)
         {
             _blockId = blockId;
             _blockInventoryUpdate = blockInventoryUpdate;
             _blockInstanceId = blockInstanceId;
+            _gameUnlockStateData = gameUnlockStateData;
             
             var option = new OpenableInventoryItemDataStoreServiceOption()
             {
@@ -69,7 +79,15 @@ namespace Game.Block.Blocks.Machine.Inventory
         
         public bool TryGetRecipeElement(out MachineRecipeMasterElement recipe)
         {
-            return MachineRecipeMasterUtil.TryGetRecipeElement(_blockId, InputSlot, FluidInputSlot, out recipe);
+            if (!MachineRecipeMasterUtil.TryGetRecipeElement(_blockId, InputSlot, FluidInputSlot, out recipe)) return false;
+
+            // アンロックされていないレシピは機械で使用不可にする
+            // Locked recipes cannot be used by machines
+            var unlockInfo = _gameUnlockStateData.MachineRecipeUnlockStateInfos[recipe.MachineRecipeGuid];
+            if (unlockInfo.IsUnlocked) return true;
+
+            recipe = null;
+            return false;
         }
         
         public void ReduceInputSlot(MachineRecipeMasterElement recipe)
diff --git a/moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/MachineRecipeMaster.cs b/moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/MachineRecipeMaster.cs
index 99e45438d..0fbe8f889 100644
--- a/moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/MachineRecipeMaster.cs
+++ b/moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/MachineRecipeMaster.cs
@@ -1,17 +1,13 @@
 using System.Collections.Generic;
 using System.Linq;
 using Core.Item.Interface;
-using Game.Context;
 using Game.Fluid;
-using Game.UnlockState;
 using Mooresmaster.Model.MachineRecipesModule;
 
 namespace Core.Master
 {
     public static class MachineRecipeMasterUtil
     {
-        private static IGameUnlockStateDataController _cachedUnlockState;
-
         public static bool TryGetRecipeElement(
             BlockId blockId,
             IReadOnlyList<IItemStack> inputSlot,
@@ -32,23 +28,7 @@ namespace Core.Master
                 fluidIds.Add(fluidContainer.FluidId);
             }
             
-            var found = MasterHolder.MachineRecipesMaster.TryGetRecipeElement(blockId, itemIds, fluidIds, out recipe);
-
-            // アンロックされていないレシピは使用不可
-            // Locked recipes cannot be used
-            if (found)
-            {
-                _cachedUnlockState ??= ServerContext.IsInitialized ? ServerContext.GetService<IGameUnlockStateDataController>() : null;
-                if (_cachedUnlockState != null &&
-                    _cachedUnlockState.MachineRecipeUnlockStateInfos.TryGetValue(recipe.MachineRecipeGuid, out var info) &&
-                    !info.IsUnlocked)
-                {
-                    recipe = null;
-                    return false;
-                }
-            }
-
-            return found;
+            return MasterHolder.MachineRecipesMaster.TryGetRecipeElement(blockId, itemIds, fluidIds, out recipe);
         }
         
         public static bool RecipeConfirmation(
@@ -97,4 +77,4 @@ namespace Core.Master
             return true;
         }
     }
-}
\ No newline at end of file
+}
diff --git a/moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/BlockTemplateUtil.cs b/moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/BlockTemplateUtil.cs
index f73edb324..e63a0a862 100644
--- a/moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/BlockTemplateUtil.cs
+++ b/moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/BlockTemplateUtil.cs
@@ -10,6 +10,7 @@ using Game.Block.Event;
 using Game.Block.Interface;
 using Game.Block.Interface.Component;
 using Game.Context;
+using Game.UnlockState;
 using Mooresmaster.Model.BlocksModule;
 using Mooresmaster.Model.InventoryConnectsModule;
 using Newtonsoft.Json;
@@ -52,7 +53,8 @@ namespace Game.Block.Factory.BlockTemplate
                 inputTankCount,
                 innerTankCapacity,
                 blockInventoryUpdateEvent,
-                blockInstanceId
+                blockInstanceId,
+                ServerContext.GetService<IGameUnlockStateDataController>()
             );
             
             var output = new VanillaMachineOutputInventory(
@@ -132,4 +134,4 @@ namespace Game.Block.Factory.BlockTemplate
             return processor;
         }
     }
-}
\ No newline at end of file
+}
```
