using System;
using Client.Game.InGame.UI.UIState.State;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class DeleteModeTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "ui.delete_mode";

        private readonly WebSocketHub _hub;
        private readonly DeleteObjectState _state;
        private readonly IDisposable _subscription;

        public DeleteModeTopic(WebSocketHub hub, DeleteObjectState state)
        {
            _hub = hub;
            _state = state;
            _subscription = state.OnUnavailableReasonChanged.Skip(1).Subscribe(_ => Publish());
        }

        public UniTask<string> GetSnapshotJsonAsync() => UniTask.FromResult(BuildJson());
        public void Dispose() => _subscription.Dispose();
        private void Publish() => _hub.Publish(TopicName, BuildJson());

        private string BuildJson()
        {
            return WebUiJson.Serialize(new DeleteModeDto { UnavailableReason = _state.GetUnavailableReason() });
        }
    }

    public class DeleteModeDto
    {
        public string UnavailableReason;
    }
}
