using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.UI.UIState;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Topics.Hotbar;

namespace Client.WebUiHost.Game
{
    /// <summary>
    /// ホットバーのtopicと4 actionを同じ境界で登録する（前例 C4WebUiRegistration）
    /// Registers the hotbar topic and its 4 actions at one boundary (precedent: C4WebUiRegistration)
    /// </summary>
    public static class HotbarWebUiRegistration
    {
        public static void Register(WebSocketHub hub, ClientHotbarDatastore clientHotbarDatastore, PlacementTargetResolver placementTargetResolver, ClientBlueprintLibrary blueprintLibrary, PlaceSystemStateController placeSystemStateController, UIStateControl uiStateControl)
        {
            // 設置対象解決はビルドメニューと同一供給源(PlacementTargetResolver)を再利用する
            // Placement-target resolution reuses the same source as the build menu (PlacementTargetResolver)
            var hotbarTopic = new HotbarTopic(hub, clientHotbarDatastore, placementTargetResolver, blueprintLibrary, placeSystemStateController);
            hub.RegisterTopic(HotbarTopic.TopicName, hotbarTopic);

            hub.RegisterAction(new HotbarSelectActionHandler(clientHotbarDatastore, uiStateControl));
            hub.RegisterAction(new HotbarAssignActionHandler(clientHotbarDatastore));
            hub.RegisterAction(new HotbarClearActionHandler(clientHotbarDatastore));
            hub.RegisterAction(new HotbarSwapActionHandler(clientHotbarDatastore));
        }
    }
}
