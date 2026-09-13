using Client.Game.InGame.BugReport.LastSession;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions.Playtest;
using Client.WebUiHost.Game.Topics.Playtest;

namespace Client.WebUiHost.Game.Playtest
{
    /// <summary>
    /// - プレイテストの開始ゲート（topicとaction）をHubへ束ねるfacade
    /// - WebUiGameBinderより前に呼ぶ
    /// - Binds the playtest start gates' topics and actions to the Hub
    /// - Must be called before WebUiGameBinder
    /// </summary>
    public static class PlaytestGateBinder
    {
        public static CrashReportGate BindCrashReportGate(WebSocketHub hub, PreviousSessionArtifacts artifacts, ICrashBundleWriter writer)
        {
            // 異常終了なら常に確認する。退避物ゼロでも説明文だけの箱には価値があるので待機条件から外さない
            // Always ask after an unclean exit; a description-only box still has value, so an empty salvage does not skip the wait
            var startsWaiting = !artifacts.PreviousExitWasClean;
            var gate = new CrashReportGate(startsWaiting, writer, artifacts);
            hub.RegisterTopic(CrashReportGateTopic.TopicName, new CrashReportGateTopic(hub, gate));
            CrashReportGateActions.Register(hub, gate);
            return gate;
        }
    }
}
