using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Client.WebUiHost.Game.Topics;
using VContainer;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// ゲーム系トピックを WebUiHost.Hub にバインドする facade。
    /// ClientContext / ClientDIContext の準備が終わった後に 1 回呼ぶ。
    /// Facade that binds game-side topics to WebUiHost.Hub.
    /// Must be called once, after ClientContext / ClientDIContext are ready.
    /// </summary>
    public static class WebUiGameBinder
    {
        public static void BindTopics()
        {
            var hub = Boot.WebUiHost.Hub;
            if (hub == null) return;

            // DI からインベントリコントローラを取得
            // Resolve the inventory controller from DI
            var controller = ClientDIContext.DIContainer
                .DIContainerResolver
                .Resolve<LocalPlayerInventoryController>();

            // インベントリトピックを生成して Hub に登録
            // Create inventory topic and register it with the Hub
            var inventoryTopic = new InventoryTopic(hub, controller);
            hub.RegisterTopic(InventoryTopic.TopicName, inventoryTopic);
        }
    }
}
