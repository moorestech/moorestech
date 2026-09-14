using System;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Playtest;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics.Playtest
{
    /// <summary>
    /// 前回異常終了の確認待ちをsnapshotとeventで配信する
    /// Publishes the previous-crash confirmation wait as a snapshot and events
    /// </summary>
    public class CrashReportGateTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "playtest.crash_report_gate";

        private readonly WebSocketHub _hub;
        private readonly CrashReportGate _gate;
        private readonly IDisposable _waitingSubscription;

        public CrashReportGateTopic(WebSocketHub hub, CrashReportGate gate)
        {
            _hub = hub;
            _gate = gate;

            // 待機解除は1回だけ起きる離散状態。変化通知をそのまま event へ流す
            // Releasing the wait is a one-shot discrete change, so the notification maps straight to an event
            _waitingSubscription = gate.OnWaitingChanged.Subscribe(_ => _hub.Publish(TopicName, BuildJson()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _waitingSubscription.Dispose();
        }

        private string BuildJson()
        {
            return WebUiJson.Serialize(new CrashReportGateData
            {
                Waiting = _gate.IsWaitingResponse,
            });
        }

        private class CrashReportGateData
        {
            public bool Waiting;
        }
    }
}
