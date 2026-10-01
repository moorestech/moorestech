using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    internal sealed class BeltSegmentItemView : IOnBeltConveyorItem
    {
        public uint RemainingTicks { get; }
        public uint TotalTicks => BeltConstants.ItemWidth;
        public ItemId ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        public IBlockConnector StartConnector { get; }
        public IBlockConnector GoalConnector { get; }
        internal BeltSegmentItemView(BeltCellItemState state, IBlockConnector start, IBlockConnector goal)
        {
            RemainingTicks = (uint)(BeltConstants.ItemWidth - state.Progress);
            ItemId = new ItemId(state.Item.ItemId);
            ItemInstanceId = BeltTransportIdentity.ToItemInstanceId(state.Item.Guid);
            StartConnector = start; GoalConnector = goal;
        }
    }
}
