using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    // train.riding トピック: 現在の乗車状態と分岐表示用の最小データを配信する
    // train.riding topic: publishes current riding state and minimal branch presentation data.
    public sealed class TrainRidingTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "train.riding";

        private readonly WebSocketHub _hub;
        private readonly UIStateControl _uiStateControl;
        private readonly TrainHUDScreenState _trainHudState;
        private readonly IDisposable _presentationSubscription;
        private readonly IDisposable _ridingEventSubscription;

        public TrainRidingTopic(WebSocketHub hub, UIStateControl uiStateControl, TrainHUDScreenState trainHudState)
        {
            _hub = hub;
            _uiStateControl = uiStateControl;
            _trainHudState = trainHudState;
            _uiStateControl.OnStateChanged += OnUiStateChanged;
            _presentationSubscription = _trainHudState.OnPresentationChanged.Subscribe(_ => Publish());
            _ridingEventSubscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(
                RidingStateEventPacket.EventTag, OnRidingStateEvent);
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _uiStateControl.OnStateChanged -= OnUiStateChanged;
            _presentationSubscription.Dispose();
            _ridingEventSubscription.Dispose();
        }

        private void OnUiStateChanged(UIStateEnum state)
        {
            Publish();
        }

        private void OnRidingStateEvent(byte[] payload)
        {
            var message = MessagePackSerializer.Deserialize<RidingStateEventMessagePack>(payload);
            if (message.PlayerId != ClientContext.PlayerConnectionSetting.PlayerId) return;
            Publish();
        }

        private void Publish()
        {
            _hub.Publish(TopicName, BuildJson());
        }

        private string BuildJson()
        {
            var riding = _uiStateControl.CurrentState == UIStateEnum.TrainHUDScreen && _trainHudState.IsRiding;
            return WebUiJson.Serialize(new TrainRidingDto
            {
                Riding = riding,
                BranchCandidateCount = riding ? _trainHudState.BranchCandidateCount : 0,
                SelectedBranchIndex = riding ? _trainHudState.SelectedBranchIndex : 0,
            });
        }
    }

    public sealed class TrainRidingDto
    {
        public bool Riding;
        public int BranchCandidateCount;
        public int SelectedBranchIndex;
    }
}
