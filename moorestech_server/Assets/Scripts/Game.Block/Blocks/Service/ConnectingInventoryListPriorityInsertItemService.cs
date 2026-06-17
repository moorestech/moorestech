using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Connector;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.Service
{
    /// <summary>
    /// 順番にアイテムを接続先へ搬出するサービス
    /// A service that outputs items to connected targets in order.
    /// </summary>
    public class ConnectingInventoryListPriorityInsertItemService : IBlockInventoryInserter, IBlockInventoryInsertTargetState
    {
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        private readonly BlockInstanceId _sourceBlockInstanceId;

        private int _index = -1;

        public ConnectingInventoryListPriorityInsertItemService(BlockInstanceId sourceBlockInstanceId, BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            _sourceBlockInstanceId = sourceBlockInstanceId;
            _blockConnectorComponent = blockConnectorComponent;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            var targetsList = _blockConnectorComponent.ConnectedTargets.ToArray();

            // 既存のround-robin順で搬出し、indexed targetなら高速挿入を使う
            // Keep the existing round-robin order and use fast insertion for indexed targets
            for (var i = 0; i < targetsList.Length && itemStack.Id != ItemMaster.EmptyItemId; i++)
                lock (targetsList)
                {
                    AddIndex(targetsList.Length);
                    itemStack = InsertToTarget(itemStack, targetsList[_index]);
                }

            return itemStack;
        }

        public bool CanInsertToNextTarget()
        {
            var targetsList = _blockConnectorComponent.ConnectedTargets.ToArray();
            if (targetsList.Length == 0) return false;

            // indexed targetなら満杯判定をcacheからO(1)で返す
            // Indexed targets answer fullness from cache in constant time
            var target = PeekNextTarget(targetsList).Key;
            if (target is IBlockInventoryFastInsertTarget fastTarget) return fastTarget.HasInsertableSlot;
            return IsTargetMaybeInsertable(target);
        }

        public bool CanInsertItemToNextTarget(IItemStack itemStack)
        {
            var targetsList = _blockConnectorComponent.ConnectedTargets.ToArray();
            if (targetsList.Length == 0) return false;

            // item別cacheを持つtargetだけ事前判定し、非対応targetは既存挿入に任せる
            // Precheck only targets with item caches and leave other targets to existing insertion
            var target = PeekNextTarget(targetsList).Key;
            if (target is IBlockInventoryFastInsertTarget fastTarget) return fastTarget.CanInsertItem(itemStack);
            return true;
        }

        private IItemStack InsertToTarget(IItemStack itemStack, KeyValuePair<IBlockInventory, ConnectedInfo> target)
        {
            if (target.Key is IBlockInventoryFastInsertTarget fastTarget) return fastTarget.InsertItemFast(itemStack);

            // connector情報が必要なtargetには従来通りcontextを渡す
            // Keep passing connector context to targets that need it
            var context = new InsertItemContext(_sourceBlockInstanceId, target.Value.SelfConnector, target.Value.TargetConnector);
            return target.Key.InsertItem(itemStack, context);
        }

        private void AddIndex(int count)
        {
            _index++;
            if (count <= _index) _index = 0;
        }

        private KeyValuePair<IBlockInventory, ConnectedInfo> PeekNextTarget(KeyValuePair<IBlockInventory, ConnectedInfo>[] targetsList)
        {
            var nextIndex = _index + 1;
            if (targetsList.Length <= nextIndex) nextIndex = 0;
            return targetsList[nextIndex];
        }

        private static bool IsTargetMaybeInsertable(IBlockInventory target)
        {
            // 非indexed targetは従来通りslotを見て満杯だけを検出する
            // For non-indexed targets, scan slots only to detect total fullness
            for (var i = 0; i < target.GetSlotSize(); i++)
            {
                var itemStack = target.GetItem(i);
                if (itemStack.Id == ItemMaster.EmptyItemId || itemStack.Count == 0) return true;

                var maxStack = MasterHolder.ItemMaster.GetItemMaster(itemStack.Id).MaxStack;
                if (itemStack.Count < maxStack) return true;
            }

            return false;
        }
    }
}
