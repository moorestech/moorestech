using Game.Block.Interface.BlockConfig;
using UnityEngine;

namespace Client.Game.Block
{
    public class BlockPreviewObject : MonoBehaviour
    {
        public BlockConfigData BlockConfig { get; private set; }
        
        public void Initialize(BlockConfigData blockConfigData)
        {
            BlockConfig = blockConfigData;
        }
    }
}