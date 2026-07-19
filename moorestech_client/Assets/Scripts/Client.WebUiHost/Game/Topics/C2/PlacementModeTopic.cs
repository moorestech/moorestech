using System;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Electric;
using Client.Game.InGame.UI.UIState.State;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Core.Master;
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
        private readonly DisplayEnergizedRange _range;
        private readonly CompositeDisposable _subscriptions = new();

        public PlacementModeTopic(WebSocketHub hub, PlaceSystemStateController controller, PlaceBlockState state, DisplayEnergizedRange range)
        {
            _hub = hub;
            _controller = controller;
            _state = state;
            _range = range;

            // HUD入力の変化だけを購読して完全snapshotを再配信する
            // Republish the complete snapshot only when a HUD input changes
            controller.OnTargetChanged.Subscribe(_ => Publish()).AddTo(_subscriptions);
            state.OnPlacementHeightChanged.Skip(1).Subscribe(_ => Publish()).AddTo(_subscriptions);
            range.OnRangeVisibleChanged.Skip(1).Subscribe(_ => Publish()).AddTo(_subscriptions);
        }

        public UniTask<string> GetSnapshotJsonAsync() => UniTask.FromResult(BuildJson());
        public void Dispose() => _subscriptions.Dispose();

        private void Publish() => _hub.Publish(TopicName, BuildJson());

        private string BuildJson()
        {
            var selectedName = GetSelectedName();
            return WebUiJson.Serialize(new PlacementModeDto
            {
                SelectedName = selectedName,
                Height = _state.GetPlacementHeight(),
                UnavailableReason = "",
                EnergizedRangeVisible = _range.IsRangeVisible(),
            });

            #region Internal

            string GetSelectedName()
            {
                var target = _controller.CurrentTarget;
                if (target is BlockPlacementTarget block) return MasterHolder.BlockMaster.GetBlockMaster(block.BlockId).Name;
                if (target is BlueprintPlacementTarget blueprint) return blueprint.BlueprintName;
                if (target is ConnectToolPlacementTarget tool) return MasterHolder.ConnectToolMaster.GetElementOrNull(tool.ConnectToolGuid)?.Name ?? "";
                if (target is TrainCarPlacementTarget) return "Train Car";
                if (target is BlueprintCopyToolPlacementTarget) return "Blueprint Copy";
                return "";
            }

            #endregion
        }
    }

    public class PlacementModeDto
    {
        public string SelectedName;
        public int Height;
        public string UnavailableReason;
        public bool EnergizedRangeVisible;
    }
}
