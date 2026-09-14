using System;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Playtest;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics.Playtest
{
    /// <summary>
    /// 初回起動の同意表示の待機をsnapshotとeventで配信する
    /// Publishes the first-boot consent notice's wait as a snapshot and events
    /// </summary>
    public class PlaytestConsentGateTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "playtest.consent_gate";

        private readonly WebSocketHub _hub;
        private readonly PlaytestConsentGate _gate;
        private readonly IDisposable _waitingSubscription;

        public PlaytestConsentGateTopic(WebSocketHub hub, PlaytestConsentGate gate)
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
            return WebUiJson.Serialize(new PlaytestConsentGateData
            {
                Waiting = _gate.IsWaitingAcknowledgement,
            });
        }

        private class PlaytestConsentGateData
        {
            public bool Waiting;
        }
    }
}
