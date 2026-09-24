using System;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.EventMode
{
    /// <summary>
    /// 出展モードの言語選択ゲートの待機をsnapshotとeventで配信する。開始を止めるゲートはこれ1枚なので順序は載せない（ADR 0065）
    /// Publishes the wait of event mode's language gate as a snapshot and events; it is the only gate holding the start, so no order travels with it (ADR 0065)
    /// </summary>
    internal sealed class EventLanguageGateTopic : ITopicHandler, IDisposable
    {
        // プレイテストの同意と前回異常終了の確認はタイトル（uGUI）へ移り、WebUIに残るゲートはこれだけ
        // The playtest consent and crash confirmation moved to the title (uGUI), leaving this as the only WebUI gate
        internal const string TopicName = "event_mode.language_gate";

        private readonly WebSocketHub _hub;
        private readonly EventLanguageGate _gate;
        private readonly IDisposable _waitingSubscription;

        internal EventLanguageGateTopic(WebSocketHub hub, EventLanguageGate gate)
        {
            _hub = hub;
            _gate = gate;

            // 待機の変化は離散状態。変化通知をそのまま event へ流す
            // A waiting change is a discrete state, so the notification maps straight to an event
            _waitingSubscription = gate.OnWaitingChanged.Subscribe(_ => _hub.Publish(TopicName, BuildJson()));
        }

        // topic名を2回書かせないための登録口
        // The registration port, so the topic name is never written twice
        internal static void Register(WebSocketHub hub, EventLanguageGate gate)
        {
            hub.RegisterTopic(TopicName, new EventLanguageGateTopic(hub, gate));
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
            return WebUiJson.Serialize(new WaitingGateData
            {
                Waiting = _gate.IsWaitingSelection,
            });
        }

        private class WaitingGateData
        {
            public bool Waiting;
        }
    }
}
