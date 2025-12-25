using System.Linq;
using Core.Item.Interface;
using Game.Block.Blocks.Connector;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlockConnectInfoModule;

namespace Game.Block.Blocks.BeltConveyor
{
    public interface IBeltConveyorBlockInventoryInserter : IBlockInventoryInserter
    {
        IItemStack InsertItem(IItemStack itemStack, BlockConnectInfoElement goalConnector);
        BlockConnectInfoElement GetFirstGoalConnector();
        bool IsValidGoalConnector(BlockConnectInfoElement goalConnector);
        int ConnectedCount { get; }
    }
    
    public class VanillaBeltConveyorBlockInventoryInserter : IBeltConveyorBlockInventoryInserter
    {
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        private readonly BlockInstanceId _sourceBlockInstanceId;

        public VanillaBeltConveyorBlockInventoryInserter(BlockInstanceId sourceBlockInstanceId, BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            _sourceBlockInstanceId = sourceBlockInstanceId;
            _blockConnectorComponent = blockConnectorComponent;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            var targets = _blockConnectorComponent.ConnectedTargets;
            if (targets.Count == 0) return itemStack;

            var connector = targets.First();

            // ConnectedInfoからBlockConnectInfoElementを取得
            // Get BlockConnectInfoElement from ConnectedInfo
            var context = new InsertItemContext(_sourceBlockInstanceId, connector.Value.SelfConnector, connector.Value.TargetConnector);

            return connector.Key.InsertItem(itemStack, context);
        }

        /// <summary>
        /// 特定のGoalConnectorを指定して出力
        /// Insert item to specific goal connector
        /// </summary>
        public IItemStack InsertItem(IItemStack itemStack, BlockConnectInfoElement goalConnector)
        {
            var targets = _blockConnectorComponent.ConnectedTargets;
            if (targets.Count == 0) return itemStack;

            foreach (var target in targets)
            {
                if (target.Value.SelfConnector == goalConnector)
                {
                    var context = new InsertItemContext(_sourceBlockInstanceId, target.Value.SelfConnector, target.Value.TargetConnector);
                    return target.Key.InsertItem(itemStack, context);
                }
            }

            // 見つからない場合はFirst()で出力
            // Use First() if not found
            return InsertItem(itemStack);
        }

        /// <summary>
        /// 最初のGoalConnectorを取得
        /// Get first goal connector
        /// </summary>
        public BlockConnectInfoElement GetFirstGoalConnector()
        {
            var targets = _blockConnectorComponent.ConnectedTargets;
            if (targets.Count == 0) return null;
            return targets.First().Value.SelfConnector;
        }

        /// <summary>
        /// 指定されたGoalConnectorが有効かどうかを確認
        /// Check if specified GoalConnector is valid
        /// </summary>
        public bool IsValidGoalConnector(BlockConnectInfoElement goalConnector)
        {
            if (goalConnector == null) return false;
            
            foreach (var target in _blockConnectorComponent.ConnectedTargets)
            {
                if (target.Value.SelfConnector == goalConnector) return true;
            }
            return false;
        }

        /// <summary>
        /// 接続されているコネクターの数を取得
        /// Get the number of connected connectors
        /// </summary>
        public int ConnectedCount => _blockConnectorComponent.ConnectedTargets.Count;
    }
}
