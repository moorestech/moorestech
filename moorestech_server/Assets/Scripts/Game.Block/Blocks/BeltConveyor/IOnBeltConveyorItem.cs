using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.BeltConveyor
{
    public interface IOnBeltConveyorItem
    {
        public uint RemainingTicks { get; }
        public uint TotalTicks { get; }
        public ItemId ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        public IBlockConnector StartConnector { get; }
        public IBlockConnector GoalConnector { get; }
    }

}
