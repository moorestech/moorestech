using Core.BeltTransport;
using Core.Master;
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

        // sourceBlockInstanceIdは搬出するベルコンのblock。コネクター対は接続から引き継ぐ
        // sourceBlockInstanceId is the emitting belt block; the connector pair comes from the connection
        public BeltMachineReceiver(BlockInstanceId sourceBlockInstanceId, in BeltTopologyConnection connection)
        {
            _inventory = connection.ReceiverInventory;
            _context = new InsertItemContext(sourceBlockInstanceId, connection.SourceConnector, connection.TargetConnector);
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
            var stack = ServerContext.ItemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId);
            var remaining = _inventory.InsertItem(stack, _context);
            return remaining.Id == ItemMaster.EmptyItemId;
        }
    }
}
