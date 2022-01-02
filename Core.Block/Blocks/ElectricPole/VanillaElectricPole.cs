using System;
using Core.Electric;

namespace Core.Block.Blocks.ElectricPole
{
    public class VanillaElectricPole : IElectricPole ,IBlock
    {
        private readonly int _intId;
        private readonly int _blockId;

        public VanillaElectricPole(int blockId,int intId)
        {
            _intId = intId;
            _blockId = blockId;
        }

        public int GetIntId()
        {
            return _intId;
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