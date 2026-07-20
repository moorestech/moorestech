using Client.Skit.UI;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions;
using Client.WebUiHost.Game.Topics;

namespace Client.WebUiHost.Game
{
    public static class C4WebUiRegistration
    {
        public static void Register(WebSocketHub hub)
        {
            // C4のpresentation topicとintent actionを同じ境界で登録する
            // Register C4 presentation topics and intent actions at one boundary
            hub.RegisterTopic(GameStateTopic.TopicName, new GameStateTopic(hub));
            hub.RegisterTopic(TutorialPresentationTopic.TopicName, new TutorialPresentationTopic(hub));
            hub.RegisterTopic(SkitPresentationTopic.TopicName, new SkitPresentationTopic(hub));
            hub.RegisterAction(new TutorialAnchorAckAction());
            hub.RegisterAction(new SkitAdvanceActionHandler(SkitPresentationStateStore.Instance));
            hub.RegisterAction(new SkitSelectActionHandler(SkitPresentationStateStore.Instance));
            hub.RegisterAction(new SkitSetAutoActionHandler(SkitPresentationStateStore.Instance));
            hub.RegisterAction(new SkitSkipActionHandler(SkitPresentationStateStore.Instance));
            hub.RegisterAction(new SkitSetUiHiddenActionHandler(SkitPresentationStateStore.Instance));
        }
    }
}
