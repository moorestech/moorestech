using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.UnlockState;
using Mooresmaster.Model.ItemsModule;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// recipe_viewer.item_list トピック: uGUI ItemListView の表示フィルタ準拠のアイテムID一覧。アンロックイベントで再配信
    /// recipe_viewer.item_list topic: item ids filtered like uGUI ItemListView; republished on unlock events
    /// </summary>
    public class RecipeViewerItemListTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "recipe_viewer.item_list";

        private readonly WebSocketHub _hub;
        private readonly ItemRecipeViewerDataContainer _recipeContainer;
        private readonly IGameUnlockStateData _unlockStateData;
        private readonly IDisposable _subscription;

        // 未知のRecipeViewTypeの警告ログを1回に抑制するためのフラグ
        // Flag to emit the unknown-RecipeViewType warning only once
        private bool _unknownViewTypeWarned;

        public RecipeViewerItemListTopic(WebSocketHub hub, ItemRecipeViewerDataContainer recipeContainer, IGameUnlockStateData unlockStateData)
        {
            _hub = hub;
            _recipeContainer = recipeContainer;
            _unlockStateData = unlockStateData;

            // ClientGameUnlockStateData より後に購読登録されるため、ここでは更新後の状態を読める
            // Subscribed after ClientGameUnlockStateData, so the updated unlock state is visible here
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(UnlockedEventPacket.EventTag, _ => _hub.Publish(TopicName, BuildJson()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private string BuildJson()
        {
            // GetItemAllIds の列挙順を維持して表示対象のみ詰める
            // Keep GetItemAllIds enumeration order and collect only visible items
            var dto = new RecipeViewerItemListDto { ItemIds = new List<int>() };
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
                if (!IsShow(itemId, itemMaster)) continue;
                dto.ItemIds.Add(itemId.AsPrimitive());
            }
            return WebUiJson.Serialize(dto);

            #region Internal

            // uGUI ItemListView.IsShow の移植。DebugParametersの強制表示分岐はtopicがデバッグ非依存のため移植しない（uGUIからの逸脱）
            // Port of uGUI ItemListView.IsShow. The DebugParameters force-show branch is intentionally not ported since topics are debug-independent (deviation from uGUI)
            bool IsShow(ItemId itemId, ItemMasterElement itemMaster)
            {
                if (itemMaster.RecipeViewType is ItemMasterElement.RecipeViewTypeConst.Default)
                {
                    // デフォルトはアンロックされていてレシピがあれば表示する（クラフトまたは機械レシピ）
                    // Default is to display if unlocked and has a recipe (craft or machine)
                    if (!IsItemUnlocked(itemId)) return false;

                    var itemRecipes = _recipeContainer.GetItem(itemId);
                    if (itemRecipes == null) return false;

                    var hasUnlockedCraftRecipe = itemRecipes.UnlockedCraftRecipes().Count != 0;
                    var hasUnlockedMachineRecipe = itemRecipes.UnlockedMachineRecipes().Count != 0;
                    return hasUnlockedCraftRecipe || hasUnlockedMachineRecipe;
                }

                if (itemMaster.RecipeViewType is ItemMasterElement.RecipeViewTypeConst.IsUnlocked)
                {
                    // アンロックされていれば表示する
                    // Display if unlocked
                    return IsItemUnlocked(itemId);
                }

                if (itemMaster.RecipeViewType is ItemMasterElement.RecipeViewTypeConst.IsCraftRecipeExist)
                {
                    // アンロック済みクラフトレシピがあれば表示する
                    // Display if there is an unlocked craft recipe
                    var itemRecipes = _recipeContainer.GetItem(itemId);
                    if (itemRecipes == null) return false;
                    return itemRecipes.UnlockedCraftRecipes().Count != 0;
                }

                if (itemMaster.RecipeViewType is ItemMasterElement.RecipeViewTypeConst.ForceHide)
                {
                    return false;
                }

                if (itemMaster.RecipeViewType is ItemMasterElement.RecipeViewTypeConst.ForceShow)
                {
                    return true;
                }

                // uGUIではthrowするが、topicでは例外禁止のため非表示扱い＋警告1回に倒す（uGUIからの逸脱）
                // uGUI throws here, but topics must not throw, so fall back to hidden with a single warning (deviation from uGUI)
                if (!_unknownViewTypeWarned)
                {
                    _unknownViewTypeWarned = true;
                    Debug.LogWarning($"[WebUiHost] Unknown RecipeViewType: {itemMaster.RecipeViewType}");
                }
                return false;
            }

            bool IsItemUnlocked(ItemId itemId)
            {
                // dict に無いアイテムはロック扱いに倒して例外を避ける
                // Treat items missing from the dict as locked to avoid exceptions
                if (!_unlockStateData.ItemUnlockStateInfos.TryGetValue(itemId, out var state)) return false;
                return state.IsUnlocked;
            }

            #endregion
        }
    }

    /// <summary>
    /// recipe_viewer.item_list の配信 DTO
    /// Payload DTO for recipe_viewer.item_list
    /// </summary>
    public class RecipeViewerItemListDto
    {
        public List<int> ItemIds;
    }
}
