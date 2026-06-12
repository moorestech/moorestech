using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Client.WebUiHost.Game.Actions;
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
        public static void Bind()
        {
            var hub = Boot.WebUiHost.Hub;
            if (hub == null)
            {
                // WebUiHost が起動失敗していると topic/action が一切登録されないため痕跡を残す
                // Leave a trace when WebUiHost failed to start, since no topics/actions get bound
                UnityEngine.Debug.LogWarning("[WebUiHost] Bind skipped: hub is null (WebUiHost not started)");
                return;
            }

            // DI からインベントリコントローラを取得
            // Resolve the inventory controller from DI
            var controller = ClientDIContext.DIContainer
                .DIContainerResolver
                .Resolve<LocalPlayerInventoryController>();

            // インベントリトピックを生成して Hub に登録
            // Create inventory topic and register it with the Hub
            var inventoryTopic = new InventoryTopic(hub, controller);
            hub.RegisterTopic(InventoryTopic.TopicName, inventoryTopic);

            // action ハンドラ登録
            // Register action handlers
            hub.RegisterAction(new EchoActionHandler());
            hub.RegisterAction(new MoveItemActionHandler(controller));
            hub.RegisterAction(new SplitGrabActionHandler(controller));
            hub.RegisterAction(new CollectActionHandler(controller));
            hub.RegisterAction(new SortInventoryActionHandler(controller));
        }
    }
}
