using System;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
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
            var selectedName = GetSelectedName();
            return WebUiJson.Serialize(new PlacementModeDto
            {
                SelectedName = selectedName,
                Height = _state.GetPlacementHeight(),
                UnavailableReason = "",
            });

            #region Internal

            string GetSelectedName()
            {
                var target = _controller.CurrentTarget;
                if (target == null) return "";
                return target.Kind switch
                {
                    global::Game.PlacementTarget.PlacementTargetKind.Block =>
                        MasterHolder.BlockMaster.GetBlockMaster(((BlockPlacementTarget)target).BlockId).Name,
                    global::Game.PlacementTarget.PlacementTargetKind.Blueprint =>
                        ((BlueprintPlacementTarget)target).DisplayName,
                    global::Game.PlacementTarget.PlacementTargetKind.ConnectTool =>
                        MasterHolder.ConnectToolMaster.GetElementOrNull(((ConnectToolPlacementTarget)target).ConnectToolGuid).Name,
                    global::Game.PlacementTarget.PlacementTargetKind.TrainCar =>
                        GetTrainCarMasterName((TrainCarPlacementTarget)target),
                    global::Game.PlacementTarget.PlacementTargetKind.BuildTool =>
                        MasterHolder.BuildToolMaster.GetBuildTool(target.Id).Name,
                    _ => throw new ArgumentOutOfRangeException(nameof(target.Kind), target.Kind, null),
                };
            }

            string GetTrainCarMasterName(TrainCarPlacementTarget target)
            {
                MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(target.TrainCarGuid, out var trainCar);
                return trainCar.Name;
            }

            #endregion
        }
    }

    public class PlacementModeDto
    {
        public string SelectedName;
        public int Height;
        public string UnavailableReason;
    }
}
