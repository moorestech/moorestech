using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Topics;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// 列車・駅・プラットフォームのactionを同じ境界で登録する（前例 HotbarWebUiRegistration）
    /// Registers train, station, and platform actions at one boundary (precedent: HotbarWebUiRegistration)
    /// </summary>
    public static class TrainWebUiActionRegistration
    {
        public static void Register(WebSocketHub hub, SubInventoryState subInventoryState, TrainUnitClientCache trainUnitClientCache, TrainTimetableFetcher timetableFetcher)
        {
            hub.RegisterAction(new TrainPlatformSetTransferModeActionHandler(subInventoryState));
            hub.RegisterAction(new TrainTimetableOpenActionHandler(subInventoryState, trainUnitClientCache, timetableFetcher));
            hub.RegisterAction(new TrainTimetableReplaceActionHandler(subInventoryState, trainUnitClientCache));
            hub.RegisterAction(new TrainTimetableSetAutoRunActionHandler(subInventoryState, trainUnitClientCache));
            hub.RegisterAction(new TrainStationSetNameActionHandler(subInventoryState));
        }
    }
}
