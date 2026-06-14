using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Block;
using Client.Network.API;
using Core.Item.Interface;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    public class BlockSubInventorySource : ISubInventorySource
    {
        public InventoryIdentifierMessagePack InventoryIdentifier { get; }
        public string UIPrefabAddressablePath => _blockGameObject.BlockMasterElement.BlockUIAddressablesPath;

        // Web UI などがブロック種別・表示名・座標識別子を読むための公開口
        // Public accessors so the Web UI can read the block type, display name, and position identifier
        public string BlockName => _blockGameObject.BlockMasterElement.Name;
        public string BlockTypeName => _blockGameObject.BlockMasterElement.BlockType;
        public Vector3Int BlockPosition => _blockGameObject.BlockPosInfo.OriginalPos;

        private readonly BlockGameObject _blockGameObject;
        
        public BlockSubInventorySource(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            InventoryIdentifier = InventoryIdentifierMessagePack.CreateBlockMessage(blockGameObject.BlockPosInfo.OriginalPos);
        }
        
        public void ExecuteInitialize(ISubInventoryView subInventoryView, InventoryResponse inventoryResponse)
        {
            ((IBlockInventoryView)subInventoryView).Initialize(_blockGameObject);
            
            if (inventoryResponse.Result != InventoryRequestResult.Success)
            {
                subInventoryView.UpdateItemList(new List<IItemStack>());
                Debug.Log($"ブロックインベントリの取得に失敗しました。結果:{inventoryResponse.Result} 位置:{InventoryIdentifier.BlockPosition.Vector3Int}");
                return;
            }

            subInventoryView.UpdateItemList(inventoryResponse.Items);
        }
    }

}
