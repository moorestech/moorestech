using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions.EventMode;
using Client.WebUiHost.Game.Topics.EventMode;

namespace Client.WebUiHost.Game.EventMode
{
    /// <summary>
    /// ゲートの topic と action を Hub へ束ねる facade。WebUiGameBinder より前に呼ばれる。
    /// Facade binding the gate's topic and action to the Hub; called before WebUiGameBinder.
    /// </summary>
    public static class EventLanguageGateBinder
    {
        public static EventLanguageGate Bind(WebSocketHub hub)
        {
            var gate = new EventLanguageGate();
            hub.RegisterTopic(EventLanguageGateTopic.TopicName, new EventLanguageGateTopic(hub, gate));
            EventLanguageGateActions.Register(hub, gate);
            return gate;
        }
    }
}
