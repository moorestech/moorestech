using Game.Block.Config;
using Game.Block.Interface.BlockConfig;
using UnityEngine;

namespace MainGame.ModLoader.Glb
{
    public class BlockGameObject : MonoBehaviour
    {
        public int BlockId { get; private set; }
        public BlockConfigData BlockConfig { get; private set; }
        public Vector2Int BlockPosition { get; private set; } = Vector2Int.zero;
        public IBlockStateChangeProcessor BlockStateChangeProcessor { get; private set; }

        public void Initialize(BlockConfigData blockConfig, Vector2Int position, IBlockStateChangeProcessor blockStateChangeProcessor)
        {
            BlockPosition = position;
            BlockId = blockConfig.BlockId;
            BlockConfig = blockConfig;
            BlockStateChangeProcessor = blockStateChangeProcessor;

            foreach (var child in gameObject.GetComponentsInChildren<BlockGameObjectChild>()) child.Init(this);
        }
    }
}