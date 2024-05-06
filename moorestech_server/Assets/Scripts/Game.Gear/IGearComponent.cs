using System.Collections.Generic;
using Game.Block.Interface.Component;

namespace Game.Gear
{
    public interface IGearComponent : IBlockComponent
    {
        public int TeethCount { get; }
        public int EntityId { get; }
        public IReadOnlyList<IGearComponent> ConnectingGears { get; }
    }
}