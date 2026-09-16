using System;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.StartGates
{
    /// <summary>
    /// 開始ゲート1枚の待機をsnapshotとeventで配信する。3枚とも同じ形で、違うのはtopic名・順番・ゲート本体だけ
    /// Publishes one start gate's wait as a snapshot and events; all three share this shape and differ only in topic name, precedence and gate
    /// </summary>
    internal sealed class WaitingGateTopic : ITopicHandler, IDisposable
    {
        private readonly WebSocketHub _hub;
        private readonly string _topicName;
        private readonly int _precedence;
        private readonly IStartGateWaitState _gate;
        private readonly IDisposable _waitingSubscription;

        internal WaitingGateTopic(WebSocketHub hub, string topicName, int precedence, IStartGateWaitState gate)
        {
            _hub = hub;
            _topicName = topicName;
            _precedence = precedence;
            _gate = gate;

            // 待機の変化は離散状態。変化通知をそのまま event へ流す
            // A waiting change is a discrete state, so the notification maps straight to an event
            _waitingSubscription = gate.OnWaitingChanged.Subscribe(_ => _hub.Publish(_topicName, BuildJson()));
        }

        // topic名を2回書かせないための登録口
        // The registration port, so the topic name is never written twice
        internal static void Register(WebSocketHub hub, string topicName, int precedence, IStartGateWaitState gate)
        {
            hub.RegisterTopic(topicName, new WaitingGateTopic(hub, topicName, precedence, gate));
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
                Waiting = _gate.IsWaiting,
                Precedence = _precedence,
            });
        }

        private class WaitingGateData
        {
            public bool Waiting;
            public int Precedence;
        }
    }
}
