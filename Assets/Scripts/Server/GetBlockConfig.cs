using Core.Block.Config;
using Core.Block.Config.LoadConfig;
using UnityEngine;

namespace Server
{
    public class GetBlockConfig : MonoBehaviour
    {
        [SerializeField] private TextAsset blockConfigText;
        
        BlockConfig _blockConfig;
        
        private void Awake()
        {
            _blockConfig = new BlockConfig(blockConfigText.text);
        }
        
        public BlockConfigData Get(int blockId)
        {
            return _blockConfig.GetBlockConfig(blockId);
        }
    }
}