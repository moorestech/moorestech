using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // segmentやbufferの搬出先となる機械。搬出のたびに機械のInsertItemで受け入れを判定し、入った場合だけ成功と答える
    // A machine as the output target of a segment or buffer; each handoff is settled by the machine's InsertItem and succeeds only when the item went in
    public sealed class BeltMachineReceiver : IBeltReceiver
    {
        private readonly IBlockInventory _inventory;
        private readonly InsertItemContext _context;
        // 搬出元のsegment番号と搬出方向。成立した搬出を差分としてこの組で記録する
        // The emitting segment number and output direction; a settled handoff is recorded as a diff under this pair
        private readonly int _segmentIndex;
        private readonly BeltDirection _outputDirection;
        private readonly BeltTransportDiffRecorder _diffRecorder;
        // 拒否されて出口で待つ個体のスタック。同じ個体が待ち続ける間は作り直さない
        // The stack of the item rejected and waiting at the exit; not recreated while the same item keeps waiting
        private IItemStack _waitingStack;
        private ItemInstanceId _waitingInstanceId;

        // sourceBlockInstanceIdは搬出するベルコンのblock。コネクター対は接続から引き継ぐ
        // sourceBlockInstanceId is the emitting belt block; the connector pair comes from the connection
        public BeltMachineReceiver(BlockInstanceId sourceBlockInstanceId, in BeltTopologyConnection connection, int segmentIndex, BeltDirection outputDirection, BeltTransportDiffRecorder diffRecorder)
        {
            _inventory = connection.ReceiverInventory;
            _context = new InsertItemContext(sourceBlockInstanceId, connection.SourceConnector, connection.TargetConnector);
            _segmentIndex = segmentIndex;
            _outputDirection = outputDirection;
            _diffRecorder = diffRecorder;
        }

        // 機械は搬入元を問い合わせないので登録しない
        // A machine never queries its sources, so nothing is registered
        public void AttachInput(IBeltSource source, BeltDirection inputDirection)
        {
        }

        // 受け入れはTryReceiveでその都度確定するため、空きは常に1マス分と答える
        // Acceptance is settled per handoff in TryReceive, so the offer is always one full cell
        public int GetOffer(BeltDirection inputDirection)
        {
            return BeltConstants.ItemWidth;
        }

        public bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item)
        {
            if (_waitingStack == null || _waitingInstanceId != item.ItemInstanceId)
            {
                _waitingStack = ServerContext.ItemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId);
                _waitingInstanceId = item.ItemInstanceId;
            }
            var remaining = _inventory.InsertItem(_waitingStack, _context);
            var accepted = remaining.Id == ItemMaster.EmptyItemId;
            if (!accepted) return false;

            // 入った事実だけが同期の対象。複製は同じtickにこの搬出を成功として再生する
            // Only the fact that it went in is synchronized; the replica replays this handoff as a success on the same tick
            _waitingStack = null;
            _diffRecorder.RecordExtract(_segmentIndex, _outputDirection);
            return true;
        }
    }
}
