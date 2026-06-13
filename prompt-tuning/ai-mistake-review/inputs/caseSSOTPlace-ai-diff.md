# AIコミット diff (base->ai) — レビュー対象
```diff
commit 5d4f00f0ac9f4a7a59540e17c1c224ec182a5cf8
Author: sakastudio <sakastudio100@gmail.com>
Date:   Thu Mar 19 14:31:30 2026 +0900

    レビュー指摘修正: キャッシュ化・冗長呼び出し削減・命名改善
    
    - GetServiceの毎tick呼び出しをキャッシュ化（ホットパス最適化）
    - UnlockedMachineRecipes()を1回計算して各ビューに渡すように変更
    - UnlockedMachineRecipes()のフォールバック動作をUnlockedCraftRecipes()と統一
    - UnlockEventMessagePackコンストラクタのパラメータ名を汎用的に修正
    - MachineRecipeView.csの完全修飾型名をusing追加で解消
    
    Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>

diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/ItemRecipeViewerDataContainer.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/ItemRecipeViewerDataContainer.cs
index 3efb0e6d6..bcb8ded93 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/ItemRecipeViewerDataContainer.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/ItemRecipeViewerDataContainer.cs
@@ -126,7 +126,7 @@ namespace Client.Game.InGame.UI.Inventory.RecipeViewer
             {
                 var blockId = kv.Key;
                 var unlockedRecipes = kv.Value
-                    .Where(m => !infos.TryGetValue(m.MachineRecipeGuid, out var info) || info.IsUnlocked)
+                    .Where(m => infos[m.MachineRecipeGuid].IsUnlocked)
                     .ToList();
                 if (unlockedRecipes.Count > 0)
                 {
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/MachineRecipeView.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/MachineRecipeView.cs
index 9b0989660..7382297cb 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/MachineRecipeView.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/MachineRecipeView.cs
@@ -4,6 +4,7 @@ using System.Linq;
 using Client.Game.InGame.Context;
 using Client.Game.InGame.UI.Inventory.Common;
 using Core.Master;
+using Mooresmaster.Model.MachineRecipesModule;
 using TMPro;
 using UniRx;
 using UnityEngine;
@@ -33,7 +34,7 @@ namespace Client.Game.InGame.UI.Inventory.RecipeViewer
         
         private int MachineRecipeCount => _currentUnlockedMachineRecipes[_currentBlockId].Count;
         private RecipeViewerItemRecipes _currentItemRecipes;
-        private Dictionary<BlockId, List<Mooresmaster.Model.MachineRecipesModule.MachineRecipeMasterElement>> _currentUnlockedMachineRecipes = new();
+        private Dictionary<BlockId, List<MachineRecipeMasterElement>> _currentUnlockedMachineRecipes = new();
         private BlockId _currentBlockId;
         private int _currentIndex;
         
@@ -58,10 +59,10 @@ namespace Client.Game.InGame.UI.Inventory.RecipeViewer
             });
         }
         
-        public void SetRecipes(RecipeViewerItemRecipes recipeViewerItemRecipes)
+        public void SetRecipes(RecipeViewerItemRecipes recipeViewerItemRecipes, Dictionary<BlockId, List<MachineRecipeMasterElement>> unlockedMachineRecipes)
         {
             _currentItemRecipes = recipeViewerItemRecipes;
-            _currentUnlockedMachineRecipes = recipeViewerItemRecipes.UnlockedMachineRecipes();
+            _currentUnlockedMachineRecipes = unlockedMachineRecipes;
             _currentIndex = 0;
             if (_currentUnlockedMachineRecipes.Count != 0)
             {
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/RecipeTabView.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/RecipeTabView.cs
index 0c9405b1d..bade88a32 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/RecipeTabView.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/RecipeTabView.cs
@@ -3,6 +3,7 @@ using System.Collections.Generic;
 using Client.Game.InGame.Context;
 using Client.Game.InGame.UnlockState;
 using Core.Master;
+using Mooresmaster.Model.MachineRecipesModule;
 using UniRx;
 using UnityEngine;
 using UnityEngine.UI;
@@ -21,16 +22,16 @@ namespace Client.Game.InGame.UI.Inventory.RecipeViewer
         
         private readonly List<RecipeViewerTabElement> _currentTabs = new();
         
-        public void SetRecipeTabView(RecipeViewerItemRecipes recipes)
+        public void SetRecipeTabView(RecipeViewerItemRecipes recipes, Dictionary<BlockId, List<MachineRecipeMasterElement>> unlockedMachineRecipes)
         {
             foreach (var tab in _currentTabs)
             {
                 Destroy(tab.gameObject);
             }
-            
+
             _currentTabs.Clear();
-            
-            // クラフトタブがあればそれを優先的異選択
+
+            // クラフトタブがあればそれを優先的に選択
             // If there is a craft tab, select it preferentially
             var isFirstCraft = false;
             var unlockedRecipe = recipes.UnlockedCraftRecipes();
@@ -44,10 +45,9 @@ namespace Client.Game.InGame.UI.Inventory.RecipeViewer
                 _currentTabs.Add(tabElement);
                 isFirstCraft = true;
             }
-            
+
             // アンロック済みの機械レシピのみタブに表示
             // Only show unlocked machine recipes in tabs
-            var unlockedMachineRecipes = recipes.UnlockedMachineRecipes();
             var isFirst = true;
             foreach (var machineRecipe in unlockedMachineRecipes)
             {
diff --git a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/RecipeViewerView.cs b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/RecipeViewerView.cs
index f4df041ca..3f10f5374 100644
--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/RecipeViewerView.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/RecipeViewerView.cs
@@ -47,21 +47,24 @@ namespace Client.Game.InGame.UI.Inventory.RecipeViewer
             }
             
             _currentRecipe = recipeViewerItemRecipes;
-            
+
+            // アンロック済みレシピを1回だけ取得して各ビューに渡す
+            // Compute unlocked machine recipes once and pass to each view
+            var unlockedMachineRecipes = recipeViewerItemRecipes.UnlockedMachineRecipes();
+
             craftInventoryView.SetRecipes(recipeViewerItemRecipes);
-            machineRecipeView.SetRecipes(recipeViewerItemRecipes);
-            recipeTabView.SetRecipeTabView(recipeViewerItemRecipes);
-            
+            machineRecipeView.SetRecipes(recipeViewerItemRecipes, unlockedMachineRecipes);
+            recipeTabView.SetRecipeTabView(recipeViewerItemRecipes, unlockedMachineRecipes);
+
             // クラフトレシピがある場合はそれを最初に表示する
+            // Show craft recipes first if available
             var isFirstCraft = recipeViewerItemRecipes.UnlockedCraftRecipes().Count != 0;
             craftInventoryView.SetActive(isFirstCraft);
             machineRecipeView.SetActive(!isFirstCraft);
-            
-            // SetRecipesの中で最初のレシピが自動選択されるようになったので
-            // DisplayRecipe(0)の呼び出しは不要
+
             // アンロック済み機械レシピがあれば表示
             // Show unlocked machine recipes if available
-            if (!isFirstCraft && recipeViewerItemRecipes.UnlockedMachineRecipes().Count != 0)
+            if (!isFirstCraft && unlockedMachineRecipes.Count != 0)
             {
                 machineRecipeView.DisplayRecipe(0);
             }
diff --git a/moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/MachineRecipeMaster.cs b/moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/MachineRecipeMaster.cs
index c5f95e70f..99e45438d 100644
--- a/moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/MachineRecipeMaster.cs
+++ b/moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/MachineRecipeMaster.cs
@@ -10,6 +10,8 @@ namespace Core.Master
 {
     public static class MachineRecipeMasterUtil
     {
+        private static IGameUnlockStateDataController _cachedUnlockState;
+
         public static bool TryGetRecipeElement(
             BlockId blockId,
             IReadOnlyList<IItemStack> inputSlot,
@@ -34,11 +36,11 @@ namespace Core.Master
 
             // アンロックされていないレシピは使用不可
             // Locked recipes cannot be used
-            if (found && ServerContext.IsInitialized)
+            if (found)
             {
-                var unlockState = ServerContext.GetService<IGameUnlockStateDataController>();
-                if (unlockState != null &&
-                    unlockState.MachineRecipeUnlockStateInfos.TryGetValue(recipe.MachineRecipeGuid, out var info) &&
+                _cachedUnlockState ??= ServerContext.IsInitialized ? ServerContext.GetService<IGameUnlockStateDataController>() : null;
+                if (_cachedUnlockState != null &&
+                    _cachedUnlockState.MachineRecipeUnlockStateInfos.TryGetValue(recipe.MachineRecipeGuid, out var info) &&
                     !info.IsUnlocked)
                 {
                     recipe = null;
diff --git a/moorestech_server/Assets/Scripts/Server.Event/EventReceive/UnlockedEventPacket.cs b/moorestech_server/Assets/Scripts/Server.Event/EventReceive/UnlockedEventPacket.cs
index 6ddc2ee31..e173332ca 100644
--- a/moorestech_server/Assets/Scripts/Server.Event/EventReceive/UnlockedEventPacket.cs
+++ b/moorestech_server/Assets/Scripts/Server.Event/EventReceive/UnlockedEventPacket.cs
@@ -62,19 +62,19 @@ namespace Server.Event.EventReceive
             UnlockedItemIdInt = (int)itemId;
         }
         
-        public UnlockEventMessagePack(UnlockEventType unlockEventType,Guid unlockedChallengeCategoryGuid)
+        public UnlockEventMessagePack(UnlockEventType unlockEventType, Guid guid)
         {
             UnlockEventTypeInt = (int)unlockEventType;
             switch (unlockEventType)
             {
                 case UnlockEventType.ChallengeCategory:
-                    UnlockedChallengeCategoryGuidStr = unlockedChallengeCategoryGuid.ToString();
+                    UnlockedChallengeCategoryGuidStr = guid.ToString();
                     break;
                 case UnlockEventType.CraftRecipe:
-                    UnlockedCraftRecipeGuidStr = unlockedChallengeCategoryGuid.ToString();
+                    UnlockedCraftRecipeGuidStr = guid.ToString();
                     break;
                 case UnlockEventType.MachineRecipe:
-                    UnlockedMachineRecipeGuidStr = unlockedChallengeCategoryGuid.ToString();
+                    UnlockedMachineRecipeGuidStr = guid.ToString();
                     break;
             }
         }
```
