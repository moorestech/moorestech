using System.Collections.Generic;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.BeltConveyor
{
    public interface IItemCollectableBeltConveyor : IBlockComponent
    {
        public IReadOnlyList<IOnBeltConveyorItem> BeltConveyorItems { get; }
    }
}