using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Block.Config;

namespace Core.Block
{
    public class BlockFactory
    {
        private Dictionary<string,IBlock> _blockTypes = new Dictionary<string, IBlock>();
        private IBlockConfig _allMachineBlockConfig;

        public BlockFactory(IBlockConfig allMachineBlockConfig)
        {
            _allMachineBlockConfig = allMachineBlockConfig;
        }

        public IBlock Create(int blockId,int indId)
        {
            var type = _allMachineBlockConfig.GetBlockConfig(blockId);
            return _blockTypes[type.Type].New(type,indId);
        }
        
        
        
        public void RegisterTemplateIBlock(string key,IBlock block)
        {
            _blockTypes.Add(key,block);
        }
    }
}