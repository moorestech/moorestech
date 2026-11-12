using Client.Game.InGame.Block;
using Client.Game.InGame.FluidNetwork;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.Inventory
{
    // Web帯域向けの再配信方針と前回配信したパイプ内容を保持する
    // Holds the web-bandwidth republish policy and the last published pipe contents
    internal sealed class BlockInventoryRepublishPolicy
    {
        private readonly FluidNetworkClientCache _fluidCache;
        // 方針はアダプタが持つ
        // The adapter owns the policy
        public bool SampleContinuously { get; private set; }
        public bool RepublishOnBeltWindow { get; private set; }
        // パイプの中身はブロック状態イベントで届かずクライアント側のコピーが毎tick進めるので、連続値と同じ間隔で見て、量か流体の種類が前回の配信から変わった回だけ出し直す
        // Pipe contents no longer arrive as block state events but advance in the client-side copy every tick, so they are sampled at the continuous-sample interval and republished only when the amount or fluid id changed since the last publish
        // 基準は最初のDTO作成時の値
        // Baseline is the value at first DTO build
        private BlockGameObject _trackedBlock;
        private bool _pipeBaselinePending;
        private bool _republishOnFluidReplica;
        private long _publishedPipeAmount;
        private FluidId _publishedPipeFluidId;

        public BlockInventoryRepublishPolicy(FluidNetworkClientCache fluidCache)
        {
            _fluidCache = fluidCache;
        }

        public void SetTrackedBlock(BlockGameObject block)
        {
            _trackedBlock = block;
            // 間引きと再配信は接続paramで決まる
            // Sampling and republish follow connection params
            var param = block == null ? null : block.BlockMasterElement.BlockParam;
            SampleContinuously = param is IGearConnectors;
            RepublishOnBeltWindow = param is IBeltSpeedParam;
            _republishOnFluidReplica = false;
            _pipeBaselinePending = block != null;
        }

        // 開時に未所属なら載るまで出さない
        // Not in copy at open: show nothing until it is
        public void CapturePipeBaselineIfPending(BlockGameObject block)
        {
            if (!_pipeBaselinePending) return; // silent-ok: 基準は追跡変更後の最初のDTO作成時だけ更新する / 通常配信が現在値を届ける
            _pipeBaselinePending = false;
            _republishOnFluidReplica = _fluidCache.TryGetNodeIndex(block.BlockPosInfo.OriginalPos, out var pipeNode);
            if (!_republishOnFluidReplica) return; // silent-ok: 開いた時にコピー未所属なら再検査しない / DTOがパイプ内容なしを表示する
            _publishedPipeAmount = _fluidCache.Replica.Network.NodeAmounts[pipeNode];
            _publishedPipeFluidId = _fluidCache.Replica.Network.NodeFluidIds[pipeNode];
        }

        // 前回配信と違えば控えてtrue
        // Remember and return true if different from last publish
        public bool HasPipeContentsChanged()
        {
            if (!_republishOnFluidReplica || !_fluidCache.TryGetNodeIndex(_trackedBlock.BlockPosInfo.OriginalPos, out var pipeNode)) return false; // silent-ok: 比較対象のコピーが無ければ再配信しない / 配信済みDTOを維持する
            var network = _fluidCache.Replica.Network;
            var amount = network.NodeAmounts[pipeNode];
            var fluidId = network.NodeFluidIds[pipeNode];
            if (amount == _publishedPipeAmount && fluidId == _publishedPipeFluidId) return false; // silent-ok: 配信済み内容と同じなら送信不要 / 配信済みDTOを維持する
            _publishedPipeAmount = amount;
            _publishedPipeFluidId = fluidId;
            return true;
        }

    }
}
