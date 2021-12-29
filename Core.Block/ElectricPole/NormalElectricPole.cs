using System;
using Core.Electric;

namespace Core.Block.ElectricPole
{
    public class NormalElectricPole : IElectricPole ,IBlock
    {
        private readonly int _intId;
        private readonly int _blockId;

        public NormalElectricPole(int blockId,int intId)
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