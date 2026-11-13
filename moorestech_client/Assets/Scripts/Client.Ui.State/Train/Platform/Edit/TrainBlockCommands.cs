using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 駅名・移送モードをサーバーへ送る（Web ホスト TrainStationActions・TrainPlatformActions と同じ規則。D-2）
    /// Sends station names and transfer modes to the server (same rules as the Web host TrainStationActions and TrainPlatformActions; D-2)
    /// </summary>
    public sealed class TrainBlockCommands : ITrainBlockCommands
    {
        private readonly SubInventoryState _subInventoryState;

        public TrainBlockCommands(SubInventoryState subInventoryState)
        {
            _subInventoryState = subInventoryState;
        }

        public async UniTask<UiCommandResult> SetStationName(string name)
        {
            if (!TryResolveOpenBlock(out var source, out var blockType)) return UiCommandRejected.Of(UiCommandRejectReason.BlockNotOpen);
            if (blockType != BlockTypeConst.TrainStation) return UiCommandRejected.Of(UiCommandRejectReason.TrainInvalidBlockType);

            // 応答はネットワーク由来なので null を拒否として扱う
            // The response comes from the network, so null counts as a refusal
            var response = await ClientContext.VanillaApi.Response.Train.SetTrainStationName(source.BlockPosition, name, CancellationToken.None);
            if (response == null || !response.Success) return UiCommandRejected.WithDiagnostic(UiCommandRejectReason.TrainStationNameFailed, response == null ? "no response" : response.FailureReason.ToString());
            return UiCommandAccepted.Instance;
        }

        public async UniTask<UiCommandResult> SetTransferMode(TrainPlatformTransferMode mode)
        {
            // 本文の種類はパネルと同じ登録表で判定する（blockType を並べない）
            // The body kind is judged by the same registry as the panel (no blockType list)
            if (!TryResolveOpenBlock(out var source, out var blockType)) return UiCommandRejected.Of(UiCommandRejectReason.BlockNotOpen);
            if (BlockComponentRegistry.Resolve(blockType).Body != BlockInventoryBodyKind.TrainPlatform) return UiCommandRejected.Of(UiCommandRejectReason.TrainInvalidBlockType);

            var response = await ClientContext.VanillaApi.Response.Train.SetTrainPlatformTransferMode(source.BlockPosition, TrainPlatformTransferModeConversion.ToServer(mode), CancellationToken.None);
            if (response == null || !response.Success) return UiCommandRejected.WithDiagnostic(UiCommandRejectReason.TrainPlatformModeFailed, response == null ? "no response" : response.FailureReason.ToString());
            return UiCommandAccepted.Instance;
        }

        private bool TryResolveOpenBlock(out BlockSubInventorySource source, out string blockType)
        {
            blockType = null;
            source = _subInventoryState.CurrentSubInventorySource as BlockSubInventorySource;
            return source != null && source.TryGetBlockIdentity(out _, out blockType);
        }
    }
}
