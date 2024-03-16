using Client.Game.BlockSystem.StateChange;
using Game.Block.Interface.BlockConfig;
using UnityEngine;

namespace Client.Game.Block
{
    public class BlockGameObject : MonoBehaviour
    {
        public int BlockId { get; private set; }
        public BlockConfigData BlockConfig { get; private set; }
        public Vector3Int BlockPosition { get; private set; } = Vector3Int.zero;
        public IBlockStateChangeProcessor BlockStateChangeProcessor { get; private set; }

        public void Initialize(BlockConfigData blockConfig, Vector3Int position, IBlockStateChangeProcessor blockStateChangeProcessor)
        {
            BlockPosition = position;
            BlockId = blockConfig.BlockId;
            BlockConfig = blockConfig;
            BlockStateChangeProcessor = blockStateChangeProcessor;

            foreach (var child in gameObject.GetComponentsInChildren<BlockGameObjectChild>()) child.Init(this);
        }
    }
}