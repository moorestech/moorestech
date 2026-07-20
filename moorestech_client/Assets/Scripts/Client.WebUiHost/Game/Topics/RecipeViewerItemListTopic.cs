using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
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
        private readonly IDisposable _subscription;

        // 未知のRecipeViewTypeの警告ログを1回に抑制するためのフラグ
        // Flag to emit the unknown-RecipeViewType warning only once
        private bool _unknownViewTypeWarned;

        public RecipeViewerItemListTopic(WebSocketHub hub, ItemRecipeViewerDataContainer recipeContainer)
        {
            _hub = hub;
            _recipeContainer = recipeContainer;

            // アンロックイベントで再配信する。判定は評価器が最新のアンロック状態を参照する
            // Republish on unlock events; the evaluator reads the latest unlock state during the check
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

            // 表示判定は共有評価器へ委譲。未知タイプはtopicでは例外禁止のため非表示＋警告1回に倒す（uGUIからの逸脱）
            // Visibility is delegated to the shared evaluator; unknown types fall back to hidden + one warning since topics must not throw (deviation from uGUI)
            bool IsShow(ItemId itemId, ItemMasterElement itemMaster)
            {
                var visibility = _recipeContainer.EvaluateVisibility(itemId, itemMaster);
                if (visibility == RecipeViewerItemVisibility.UnknownType)
                {
                    if (!_unknownViewTypeWarned)
                    {
                        _unknownViewTypeWarned = true;
                        Debug.LogWarning($"[WebUiHost] Unknown RecipeViewType: {itemMaster.RecipeViewType}");
                    }
                    return false;
                }
                return visibility == RecipeViewerItemVisibility.Show;
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
