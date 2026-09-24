using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions.EventMode;

namespace Client.WebUiHost.Game.EventMode
{
    /// <summary>
    /// - topicとactionをHubへ束ねるfacade
    /// - WebUiGameBinderより前に呼ぶ
    /// - Binds the gate's topic and action to the Hub
    /// - Must be called before WebUiGameBinder
    /// </summary>
    public static class EventLanguageGateBinder
    {
        public static EventLanguageGate Bind(WebSocketHub hub, bool startsWaiting)
        {
            var gate = new EventLanguageGate(startsWaiting);
            EventLanguageGateTopic.Register(hub, gate);
            EventLanguageGateActions.Register(hub, gate);
            return gate;
        }
    }
}
