using MainGame.GameLogic.Chunk;
using MainGame.UnityView.Chunk;
using MainGame.UnityView.UI.Inventory.View;
using Server;
using UnityEngine;
using UnityEngine.Serialization;

namespace MainGame.Control.UI.Inventory
{
    public class BlockInventoryInput : MonoBehaviour
    {
        [SerializeField] private BlockInventoryItemView blockInventoryItemView;
        private GetBlockConfig _getBlockConfig;
        private ChunkDataStoreCache _chunkDataStoreCache;
        
        
        
        public void OpenInventory(Vector2Int blockPos)
        {
        }
    }
}