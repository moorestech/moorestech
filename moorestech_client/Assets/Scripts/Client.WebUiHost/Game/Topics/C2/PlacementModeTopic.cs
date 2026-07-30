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
            var dto = PlacementModeDtoFactory.Create(
                _controller.CurrentTarget,
                _state.GetPlacementHeight(),
                "");
            return WebUiJson.Serialize(dto);
        }
    }

    public class PlacementModeDto
    {
        public string SelectedTargetType;
        public string SelectedBlockGuid;
        public string SelectedName;
        public int Height;
        public string UnavailableReason;
    }

    public static class PlacementModeDtoFactory
    {
        public static PlacementModeDto Create(
            IPlacementTarget target,
            int height,
            string unavailableReason)
        {
            var dto = new PlacementModeDto
            {
                Height = height,
                UnavailableReason = unavailableReason,
            };

            // ブロックとBPコピーはWeb側解決用identityだけを配信する
            // Deliver only Web-resolvable identity for blocks and the blueprint copy tool
            switch (target)
            {
                case BlockPlacementTarget block:
                    dto.SelectedTargetType = "block";
                    dto.SelectedBlockGuid = MasterHolder.BlockMaster
                        .GetBlockMaster(block.BlockId).BlockGuid.ToString("D");
                    return dto;
                case BlueprintCopyToolPlacementTarget:
                    dto.SelectedTargetType = "blueprintCopy";
                    return dto;
            }

            // ユーザー命名BPとstable key未定の対象は既存raw表示を維持する
            // Preserve existing raw labels for user blueprints and targets without stable keys
            dto.SelectedTargetType = "raw";
            dto.SelectedName = target switch
            {
                null => "",
                BlueprintPlacementTarget blueprint => blueprint.BlueprintName,
                ConnectToolPlacementTarget tool => MasterHolder.ConnectToolMaster
                    .GetElementOrNull(tool.ConnectToolGuid).Name,
                TrainCarPlacementTarget => "Train Car",
                _ => throw new InvalidOperationException(
                    $"Unsupported placement target type: {target.GetType().FullName}"),
            };
            return dto;
        }
    }
}
