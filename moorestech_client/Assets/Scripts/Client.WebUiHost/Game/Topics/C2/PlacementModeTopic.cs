using System;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.UIState.State;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using Game.PlacementTarget;
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
            controller.OnWheelOwnedByToolChanged.Skip(1).Subscribe(_ => Publish()).AddTo(_subscriptions);
        }

        public UniTask<string> GetSnapshotJsonAsync() => UniTask.FromResult(BuildJson());
        public void Dispose() => _subscriptions.Dispose();

        private void Publish() => _hub.Publish(TopicName, BuildJson());

        private string BuildJson()
        {
            var dto = PlacementModeDtoFactory.Create(
                _controller.CurrentTarget,
                _state.GetPlacementHeight(),
                "",
                _controller.IsWheelOwnedByTool);
            return WebUiJson.Serialize(dto);
        }
    }

    public class PlacementModeDto
    {
        public string SelectedTargetType;
        public string SelectedBlockGuid;
        public string SelectedConnectToolGuid;
        public string SelectedTrainCarGuid;
        public string SelectedName;
        public int Height;
        public string UnavailableReason;
        public bool WheelOwnedByTool;
    }

    public static class PlacementModeDtoFactory
    {
        public static PlacementModeDto Create(
            IPlacementTarget target,
            int height,
            string unavailableReason,
            bool wheelOwnedByTool)
        {
            var dto = new PlacementModeDto
            {
                Height = height,
                UnavailableReason = unavailableReason,
                WheelOwnedByTool = wheelOwnedByTool,
            };

            // マスタ由来対象はWeb側辞書解決用の種別とGuidだけを配信する
            // Deliver only kind and GUID for master-derived targets resolved by the Web dictionary
            if (target == null)
            {
                dto.SelectedTargetType = "raw";
                dto.SelectedName = "";
                return dto;
            }

            switch (target.Kind)
            {
                case PlacementTargetKind.Block:
                    dto.SelectedTargetType = "block";
                    dto.SelectedBlockGuid = target.Id.ToString("D");
                    return dto;
                case PlacementTargetKind.ConnectTool:
                    dto.SelectedTargetType = "connectTool";
                    dto.SelectedConnectToolGuid = target.Id.ToString("D");
                    return dto;
                case PlacementTargetKind.TrainCar:
                    dto.SelectedTargetType = "trainCar";
                    dto.SelectedTrainCarGuid = target.Id.ToString("D");
                    return dto;
                case PlacementTargetKind.BlueprintCopy:
                    dto.SelectedTargetType = "blueprintCopy";
                    return dto;
                case PlacementTargetKind.Blueprint:
                    dto.SelectedTargetType = "raw";
                    dto.SelectedName = target.DisplayName;
                    return dto;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(target.Kind), target.Kind, null);
            }
        }
    }
}
