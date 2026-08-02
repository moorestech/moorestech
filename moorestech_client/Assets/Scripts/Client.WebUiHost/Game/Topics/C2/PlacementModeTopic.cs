using System;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.UI.UIState.State;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class PlacementModeTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "ui.placement_mode";

        private readonly WebSocketHub _hub;
        private readonly PlaceSystemStateController _controller;
        private readonly PlaceBlockState _state;
        private readonly CompositeDisposable _subscriptions = new();

        public PlacementModeTopic(WebSocketHub hub, PlaceSystemStateController controller, PlaceBlockState state)
        {
            _hub = hub;
            _controller = controller;
            _state = state;

            // HUD入力の変化だけを購読して完全snapshotを再配信する
            // Republish the complete snapshot only when a HUD input changes
            controller.OnTargetChanged.Subscribe(_ => Publish()).AddTo(_subscriptions);
            state.OnPlacementHeightChanged.Skip(1).Subscribe(_ => Publish()).AddTo(_subscriptions);
        }

        public UniTask<string> GetSnapshotJsonAsync() => UniTask.FromResult(BuildJson());
        public void Dispose() => _subscriptions.Dispose();

        private void Publish() => _hub.Publish(TopicName, BuildJson());

        private string BuildJson()
        {
            return WebUiJson.Serialize(new PlacementModeDto
            {
                SelectedName = _controller.CurrentTarget?.DisplayName ?? "",
                Height = _state.GetPlacementHeight(),
                UnavailableReason = "",
            });
        }
    }

    public class PlacementModeDto
    {
        public string SelectedName;
        public int Height;
        public string UnavailableReason;
    }
}
