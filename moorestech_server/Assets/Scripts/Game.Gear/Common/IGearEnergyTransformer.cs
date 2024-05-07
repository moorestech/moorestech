using System.Collections.Generic;
using Game.Block.Interface.Component;

namespace Game.Gear.Common
{
    public interface IGearEnergyTransformer : IBlockComponent
    {
        public bool IsReverseRotation { get; }

        public int EntityId { get; }

        public IReadOnlyList<IGearEnergyTransformer> ConnectingTransformers { get; }
        
        public void SupplyRotation(float rpm, bool isClockwise);
    }
}