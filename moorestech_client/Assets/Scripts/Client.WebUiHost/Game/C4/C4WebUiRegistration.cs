using Client.Ui.State.Skit;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Topics;

namespace Client.WebUiHost.Game
{
    public static class C4WebUiRegistration
    {
        public static void Register(WebSocketHub hub, ISkitCommands skitCommands)
        {
            // C4のpresentation topicとintent actionを同じ境界で登録する。skitの操作はGameUiと同じコマンドを通す
            // Register C4 presentation topics and intent actions at one boundary; skit operations go through the same commands as GameUi
            hub.RegisterTopic(GameStateTopic.TopicName, new GameStateTopic(hub));
            hub.RegisterTopic(TutorialPresentationTopic.TopicName, new TutorialPresentationTopic(hub));
            hub.RegisterTopic(WorldPinTopic.TopicName, new WorldPinTopic(hub));
            hub.RegisterTopic(SkitPresentationTopic.TopicName, new SkitPresentationTopic(hub));
            hub.RegisterAction(new TutorialAnchorAckAction());
            hub.RegisterAction(new SkitAdvanceActionHandler(skitCommands));
            hub.RegisterAction(new SkitSelectActionHandler(skitCommands));
            hub.RegisterAction(new SkitSetAutoActionHandler(skitCommands));
            hub.RegisterAction(new SkitSkipActionHandler(skitCommands));
            hub.RegisterAction(new SkitSetUiHiddenActionHandler(skitCommands));
        }
    }
}
