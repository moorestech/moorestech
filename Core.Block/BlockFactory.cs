using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Block
{
    public class BlockFactory
    {
        private Dictionary<string,IBlock> _blockTypes = new Dictionary<string, IBlock>();
        private BlockConfig _blockConfig;

        public BlockFactory(BlockConfig blockConfig)
        {
            _blockConfig = blockConfig;
        }

        public IBlock Create(int blockId)
        {
            var type = _blockConfig.GetBlockConfigData(blockId);
            return _blockTypes[type.Type].New(type);
        }
        
        
        
        public void RegisterTemplateIBlock(string key,IBlock block)
        {
            _blockTypes.Add(key,block);
        }
    }
}