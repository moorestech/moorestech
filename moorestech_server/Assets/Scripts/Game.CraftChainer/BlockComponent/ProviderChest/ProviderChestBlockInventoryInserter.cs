using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Connector;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.Context;
using Game.CraftChainer.CraftNetwork;
using UniRx;

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
        
        private int _index = -1;
        
        public ProviderChestBlockInventoryInserter(BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            _blockConnectorComponent = blockConnectorComponent;
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            var context = CraftChainerManager.Instance.GetChainerNetworkContext(_providerChestNodeId);
            var inventory = _blockConnectorComponent.ConnectedTargets.Keys.ToArray()[_index];
            
            // 1個ずつアイテムを挿入し、それを返すため、1個分のアイテムを作成
            // Insert items one by one and return them, so create an item for one item
            var insertItem = ServerContext.ItemStackFactory.Create(itemStack.Id, 1);
            var insertResult = inventory.InsertItem(insertItem);
            
            var nextInventory = context.GetTransportNextBlock(insertItem, _providerChestNodeId, _blockConnectorComponent);
            if (nextInventory == null)
            {
                // 移動先がないのでそのまま返す
                // Return as it is because there is no destination
                return itemStack;
            }
            
            if (insertResult.Id == ItemMaster.EmptyItemId)
            {
                // アイテムが挿入できれば、元のアイテムから1個分を減らしたアイテムを返す
                // If the item can be inserted, return the item with one less item from the original item
                return itemStack.SubItem(1);
            }
            
            // 挿入失敗なのでそのまま返す。
            // Return as it is because the insertion failed.
            return itemStack;
        }
        
    }
}