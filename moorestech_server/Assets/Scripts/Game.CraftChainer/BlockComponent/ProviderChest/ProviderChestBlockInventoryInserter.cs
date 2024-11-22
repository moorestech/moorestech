using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Connector;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.Context;
using Game.CraftChainer.CraftNetwork;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.CraftChainer.BlockComponent.ProviderChest
{
    /// <summary>
    /// CraftChainerネットワークへのアイテムの供給リクエストを受け、InsertItemがそれに合致しているアイテムをCraftChainerネットワークに供給する
    /// InsertItemメソッドはチェスト等から毎フレーム叩かれているため、明示的にInsertを呼び出すと言ったことはしない。
    ///
    /// Receive a request to supply an item to the CraftChainer network, and InsertItem will supply the matching item to the CraftChainer network.
    /// The InsertItem method is hit every frame from the chest, etc., so it does not explicitly call Insert.
    /// </summary>
    public class ProviderChestBlockInventoryInserter : IBlockInventoryInserter
    {
        private readonly CraftChainerNodeId _providerChestNodeId;
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        
        public ProviderChestBlockInventoryInserter(CraftChainerNodeId providerChestNodeId, BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            _providerChestNodeId = providerChestNodeId;
            _blockConnectorComponent = blockConnectorComponent;
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            var context = CraftChainerManager.Instance.GetChainerNetworkContext(_providerChestNodeId);
            if (context == null)
            {
                return itemStack;
            }
            
            // 1個ずつアイテムを挿入し、それを返すため、1個分のアイテムを作成
            // Insert items one by one and return them, so create an item for one item
            var oneItem = ServerContext.ItemStackFactory.Create(itemStack.Id, 1);
            
            var insertResult = context.InsertNodeNetworkNextBlock(oneItem, _providerChestNodeId, _blockConnectorComponent);
            
            // アイテムが消失しないように、1個ひいたアイテムと、挿入結果のアイテムを合成して返す
            // To prevent the item from disappearing, return a composite of the item with one item subtracted and the inserted item
            var subOneItem = itemStack.SubItem(1);
            return insertResult.AddItem(subOneItem).ProcessResultItemStack;
        }
    }
}