using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Block;
using Client.Network.API;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    public class BlockSubInventorySource : ISubInventorySource
    {
        public InventoryIdentifierMessagePack InventoryIdentifier { get; }
        public string UIPrefabAddressablePath => _blockGameObject.BlockMasterElement.BlockUIAddressablesPath;
        
        private readonly BlockGameObject _blockGameObject;
        
        public BlockSubInventorySource(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            InventoryIdentifier = InventoryIdentifierMessagePack.CreateBlockMessage(blockGameObject.BlockPosInfo.OriginalPos);
        }
        
        public void ExecuteInitialize(ISubInventoryView subInventoryView, InventoryResponse inventoryResponse)
        {
            if (inventoryResponse.Result != InventoryRequestResult.Success)
            {
                Debug.LogError($"BlockSubInventorySource: inventory request failed. Result:{inventoryResponse.Result} Position:{InventoryIdentifier.BlockPosition.Vector3Int}");
                return;
            }

            ((IBlockInventoryView)subInventoryView).Initialize(_blockGameObject);
            subInventoryView.UpdateItemList(inventoryResponse.Items);
        }
    }

}
