using Client.Game.InGame.Block;
using Client.Game.InGame.FluidNetwork;
using Client.Game.InGame.UI.BlockDetail;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.Inventory
{
    // Web帯域向けの再配信方針と前回配信したパイプ内容を保持する
    // Holds the web-bandwidth republish policy and the last published pipe contents
    internal sealed class BlockInventoryRepublishPolicy
    {
        private readonly OpenBlockDetailTracker _tracker;
        private readonly FluidNetworkClientCache _fluidCache;
        // 再配信の方針はWeb帯域のためのものなのでアダプタが持つ。追跡ブロックが変わるたびに導き直す
        // Republish policy exists for web bandwidth, so the adapter owns it and re-derives it whenever the tracked block changes
        public bool SampleContinuously { get; private set; }
        public bool RepublishOnBeltWindow { get; private set; }
        // パイプの中身はブロック状態イベントで届かずクライアント側のコピーが毎tick進めるので、連続値と同じ間隔で見て、量か流体の種類が前回の配信から変わった回だけ出し直す
        // Pipe contents do not arrive as block state events but advance in the client-side copy every tick, so they are sampled at the continuous-sample interval and republished only when the amount or fluid id changed since the last publish
        // 比較の基準は、追跡が変わった後に最初にDTOを組む瞬間の値で控える（配信した中身と同じ瞬間にそろえる）
        // The comparison baseline is captured when the first DTO after a tracking change is built, so it matches the published contents
        private bool _pipeBaselinePending;
        private bool _republishOnFluidReplica;
        private long _publishedPipeAmount;
        private FluidId _publishedPipeFluidId;

        public BlockInventoryRepublishPolicy(OpenBlockDetailTracker tracker, FluidNetworkClientCache fluidCache)
        {
            _tracker = tracker;
            _fluidCache = fluidCache;
        }

        public void SetTrackedBlock(BlockGameObject block)
        {
            // ギアの連続値の間引きとベルト統計窓での出し直しは、追跡ブロックの接続paramで決まる（ネットワーク取得の判断はtrackerが持つ）
            // Gear sampling and belt-window republish follow the tracked block's connection params (the network-fetch decision belongs to the tracker)
            var param = block == null ? null : block.BlockMasterElement.BlockParam;
            SampleContinuously = param is IGearConnectors;
            RepublishOnBeltWindow = param is IBeltSpeedParam;
            _republishOnFluidReplica = false;
            _pipeBaselinePending = block != null;
        }

        // 開いた時点でコピーに載っていないパイプは、載るまで中身を出さない(調べ直さない)。載っていれば今の中身を配信済みとして控え、最初の配信は通常の開く経路に任せる
        // A pipe not yet in the copy when opened shows no contents until reopened (no re-check); when present, the current contents count as published and the first publish goes through the normal open path
        public void CapturePipeBaselineIfPending(BlockGameObject block)
        {
            if (!_pipeBaselinePending) return; // silent-ok: 基準は追跡変更後の最初のDTO作成時だけ更新する / 通常配信が現在値を届ける
            _pipeBaselinePending = false;
            _republishOnFluidReplica = _fluidCache.TryGetNodeIndex(block.BlockPosInfo.OriginalPos, out var pipeNode);
            if (!_republishOnFluidReplica) return; // silent-ok: 開いた時にコピー未所属なら再検査しない / DTOがパイプ内容なしを表示する
            _publishedPipeAmount = _fluidCache.Replica.Network.NodeAmounts[pipeNode];
            _publishedPipeFluidId = _fluidCache.Replica.Network.NodeFluidIds[pipeNode];
        }

        // 開いているパイプの量・流体の種類をクライアント側のコピーから読み、前回の配信と違えば控えて true を返す(変わらない間は送らない)
        // Read the open pipe's amount and fluid id from the client-side copy; when they differ from the last publish, remember them and return true (nothing is sent while unchanged)
        public bool HasPipeContentsChanged()
        {
            if (!_republishOnFluidReplica || !_fluidCache.TryGetNodeIndex(_tracker.TrackedBlock.BlockPosInfo.OriginalPos, out var pipeNode)) return false; // silent-ok: 比較対象のコピーが無ければ再配信しない / 配信済みDTOを維持する
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
