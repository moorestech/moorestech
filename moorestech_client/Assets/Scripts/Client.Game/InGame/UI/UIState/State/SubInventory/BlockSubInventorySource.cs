using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Block;
using Client.Network.API;
using Core.Item.Interface;
using Game.Block.Interface.State;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    public class BlockSubInventorySource : ISubInventorySource
    {
        public InventoryIdentifierMessagePack InventoryIdentifier { get; }
        public string UIPrefabAddressablePath => _blockGameObject.BlockMasterElement.BlockUIAddressablesPath;

        // ブロック種別/表示名/座標の公開口
        // Read access to block type, name, position
        public string BlockName => _blockGameObject.BlockMasterElement.Name;
        public string BlockTypeName => _blockGameObject.BlockMasterElement.BlockType;
        public Vector3Int BlockPosition => _blockGameObject.BlockPosInfo.OriginalPos;

        private readonly BlockGameObject _blockGameObject;

        public BlockSubInventorySource(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            InventoryIdentifier = InventoryIdentifierMessagePack.CreateBlockMessage(blockGameObject.BlockPosInfo.OriginalPos);
        }

        public FluidMachineInventoryStateDetail GetFluidInventoryStateOrNull()
        {
            // タンク/機械なら流体状態を返す。非対応ブロックは null
            // Return fluid state for tanks/machines; null for blocks without it
            return _blockGameObject.GetStateDetail<FluidMachineInventoryStateDetail>(FluidMachineInventoryStateDetail.BlockStateDetailKey);
        }

        public CommonMachineBlockStateDetail GetMachineStateOrNull()
        {
            // 機械なら加工進捗を含む状態を返す。非機械は null
            // Return machine state incl. processing progress; null for non-machines
            return _blockGameObject.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
        }

        public string CreateBlockStateEventTag()
        {
            // このブロックの状態変化イベントタグ（流体/進捗の再配信購読に使う）
            // This block's state-change event tag (used to subscribe for fluid/progress republish)
            return ChangeBlockStateEventPacket.CreateSpecifiedBlockEventTag(_blockGameObject.BlockPosInfo);
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
