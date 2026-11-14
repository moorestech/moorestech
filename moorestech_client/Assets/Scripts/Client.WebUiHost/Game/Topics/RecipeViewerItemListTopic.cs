using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Ui.State.Recipe.Catalog;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using Server.Event.EventReceive;

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
        private readonly RecipeViewerItemCatalog _catalog;
        private readonly IDisposable _subscription;

        public RecipeViewerItemListTopic(WebSocketHub hub, RecipeViewerItemCatalog catalog)
        {
            _hub = hub;
            _catalog = catalog;

            // アンロックイベントで再配信する。判定は Catalog が最新のアンロック状態を参照する
            // Republish on unlock events; the catalog reads the latest unlock state during the check
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
            // 表示判定・並び・未知の型の警告は State の Catalog に任せ、ここでは DTO へ写すだけ
            // The State catalog owns visibility, order and the unknown-type warning; this only maps to the DTO
            var dto = new RecipeViewerItemListDto { ItemIds = new List<int>() };
            foreach (var itemId in _catalog.BuildVisibleItems()) dto.ItemIds.Add(itemId.AsPrimitive());
            return WebUiJson.Serialize(dto);
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
