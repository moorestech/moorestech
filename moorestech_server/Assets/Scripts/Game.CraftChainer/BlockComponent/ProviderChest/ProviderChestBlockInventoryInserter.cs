using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Connector;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.Context;
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
        public IObservable<IItemStack> OnDistributedItemOnNetwork => _onDistributedItemOnNetwork;
        private readonly Subject<IItemStack> _onDistributedItemOnNetwork = new();
        
        private readonly Dictionary<ItemId,int> _distributionWaitList = new();
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        
        private int _index = -1;
        
        public void EnqueueItemDistributedOnNetwork(ItemId itemId, int count)
        {
            if (!_distributionWaitList.TryAdd(itemId, count))
            {
                _distributionWaitList[itemId] += count;
            }
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            // インサートを受けたアイテムが配布リストにあるか確認
            // Check if the item received for insert is in the distribution list
            if (!_distributionWaitList.TryGetValue(itemStack.Id, out var count))
            {
                // 配布リストにない場合はそのまま返す
                // If it is not in the distribution list, return it as it is
                return itemStack;
            }
            
            AddIndex();
            var inventory = _blockConnectorComponent.ConnectedTargets.Keys.ToArray()[_index];
            
            // 1個ずつアイテムを挿入し、それを返すため、1個分のアイテムを作成
            // Insert items one by one and return them, so create an item for one item
            var insertItem = ServerContext.ItemStackFactory.Create(itemStack.Id, 1);
            var insertResult = inventory.InsertItem(insertItem);
            
            
            if (insertResult.Id == ItemMaster.EmptyItemId)
            {
                // アイテムが挿入できれば、元のアイテムから1個分を減らしたアイテムを返す
                // If the item can be inserted, return the item with one less item from the original item
                count--;
                if (count == 0)
                {
                    _distributionWaitList.Remove(itemStack.Id);
                }
                else
                {
                    _distributionWaitList[itemStack.Id] = count;
                }
                
                _onDistributedItemOnNetwork.OnNext(insertItem);
                return itemStack.SubItem(1);
            }
            
            // 挿入失敗なのでそのまま返す。
            // Return as it is because the insertion failed.
            return itemStack;
        }
        
        
        private void AddIndex()
        {
            _index++;
            if (_blockConnectorComponent.ConnectedTargets.Count <= _index) _index = 0;
        }
    }
}