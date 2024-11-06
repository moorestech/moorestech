using System.Collections.Generic;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.BeltConveyor
{
    public interface IItemCollectableBeltConveyor : IBlockComponent
    {
        public BeltConveyorSlopeType SlopeType { get; }
        public IReadOnlyList<IOnBeltConveyorItem> BeltConveyorItems { get; }
    }
    
    public enum BeltConveyorSlopeType
    {
        Straight,
        Up,
        Down
    }
}