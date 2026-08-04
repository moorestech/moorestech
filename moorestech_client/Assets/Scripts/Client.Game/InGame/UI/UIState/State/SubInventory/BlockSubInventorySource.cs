using System;
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

        // 表示名を運ばず、表示側が辞書解決できる識別子を公開する
        // Expose identity instead of a source name so the presentation resolves its dictionary
        public Guid BlockGuid => _blockGameObject.BlockMasterElement.BlockGuid;
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
