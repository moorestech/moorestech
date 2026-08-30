using System;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.EventMode;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics.EventMode
{
    /// <summary>
    /// 言語選択待ちをsnapshotとeventで配信する
    /// Publishes the language selection wait as a snapshot and events
    /// </summary>
    public class EventLanguageGateTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "event_mode.language_gate";

        private readonly WebSocketHub _hub;
        private readonly EventLanguageGate _gate;
        private readonly IDisposable _waitingSubscription;

        public EventLanguageGateTopic(WebSocketHub hub, EventLanguageGate gate)
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
            return WebUiJson.Serialize(new EventLanguageGateData
            {
                Waiting = _gate.IsWaitingSelection,
            });
        }

        private class EventLanguageGateData
        {
            public bool Waiting;
        }
    }
}
