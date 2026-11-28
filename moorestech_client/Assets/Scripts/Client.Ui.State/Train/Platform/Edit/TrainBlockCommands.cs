using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.Commands;
using Core.Master;
using Core.Master.Validator.Block.Screen;
using Cysharp.Threading.Tasks;
using Mooresmaster.Model.BlocksModule;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 駅名・移送モードをサーバーへ送る。移送モードと駅名の送信は TrainPlatformEditCommands が呼ぶ。駅名は Web の TrainStationActions の規則の写し
    /// Sends station names and transfer modes to the server; TrainPlatformEditCommands calls it for both; station names are a port of the rules of Web TrainStationActions
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
            if (!TryResolveOpenBlock(out var source, out var master)) return UiCommandRejected.Of(UiCommandRejectReason.BlockNotOpen);
            if (master.BlockType != BlockTypeConst.TrainStation) return UiCommandRejected.Of(UiCommandRejectReason.BlockTypeMismatch);

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
            if (!TryResolveOpenBlock(out var source, out var master)) return UiCommandRejected.Of(UiCommandRejectReason.BlockNotOpen);
            if (!BlockScreens.IsOpenable(master) || BlockComponentRegistry.Resolve(master.BlockScreen).Body != BlockInventoryBodyKind.TrainPlatform) return UiCommandRejected.Of(UiCommandRejectReason.BlockTypeMismatch);

            var response = await ClientContext.VanillaApi.Response.Train.SetTrainPlatformTransferMode(source.BlockPosition, TrainPlatformTransferModeConversion.ToServer(mode), CancellationToken.None);
            if (response == null || !response.Success) return UiCommandRejected.WithDiagnostic(UiCommandRejectReason.TrainPlatformModeFailed, response == null ? "no response" : response.FailureReason.ToString());
            return UiCommandAccepted.Instance;
        }

        private bool TryResolveOpenBlock(out BlockSubInventorySource source, out BlockMasterElement master)
        {
            master = null;
            source = _subInventoryState.CurrentSubInventorySource as BlockSubInventorySource;
            if (source == null || !source.TryGetBlockIdentity(out var blockGuid, out _)) return false;
            master = MasterHolder.BlockMaster.GetBlockMaster(blockGuid);
            return true;
        }
    }
}
