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
    public class ConnectingInventoryListPriorityInsertItemService : IBlockInventoryInserter
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

        private void AddIndex(int count)
        {
            _index++;
            if (count <= _index) _index = 0;
        }
    }
}