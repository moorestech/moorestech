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
    /// 順番にアイテムに入れ続けるシステム
    /// A system that keeps putting items in order.
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
            // 接続先のリストとConnectedInfoを取得
            // Get list of connected targets and ConnectedInfo
            var connectedTargets = _blockConnectorComponent.ConnectedTargets;
            var targetsList = connectedTargets.ToArray();

            for (var i = 0; i < targetsList.Length && itemStack.Id != ItemMaster.EmptyItemId; i++)
                lock (targetsList)
                {
                    AddIndex(targetsList.Length);
                    var target = targetsList[_index];

                    // ConnectedInfoからコネクタ情報を取得してInsertItemContextを作成
                    // Create InsertItemContext from ConnectedInfo
                    var context = new InsertItemContext(_sourceBlockInstanceId, target.Value.SelfConnector, target.Value.TargetConnector);
                    itemStack = target.Key.InsertItem(itemStack, context);
                }

            return itemStack;
        }

        public bool CanInsertToNextTarget()
        {
            // 次に見る搬出先だけを判定し、搬出元スロット走査の前に詰まりを落とす
            // Check only the next output target before scanning source slots
            var targetsList = _blockConnectorComponent.ConnectedTargets.ToArray();
            if (targetsList.Length == 0) return false;

            var target = PeekNextTarget(targetsList);
            return IsTargetMaybeInsertable(target.Key);
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
            // 空きスロットか未満杯スタックがあれば、搬出元次第で入る可能性を残す
            // Keep the target open when any empty or non-full stack can accept a source item
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
