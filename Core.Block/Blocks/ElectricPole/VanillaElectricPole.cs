using System;
using Core.Electric;

namespace Core.Block.Blocks.ElectricPole
{
    public class VanillaElectricPole : IElectricPole, IBlock
    {
        private readonly int _entityId;
        private readonly int _blockId;

        public VanillaElectricPole(int blockId, int entityId)
        {
            _entityId = entityId;
            _blockId = blockId;
        }

        public int GetEntityId()
        {
            return _entityId;
        }

        public int GetBlockId()
        {
            return _blockId;
        }

        public string GetSaveState()
        {
            return String.Empty;
        }
    }
}