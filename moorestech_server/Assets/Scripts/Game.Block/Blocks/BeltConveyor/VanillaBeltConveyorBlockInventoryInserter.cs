using System;
using System.Collections.Generic;
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
        BlockConnectInfoElement GetNextGoalConnector();
        bool IsValidGoalConnector(BlockConnectInfoElement goalConnector);
        BlockConnectInfoElement GetGoalConnector(Guid connectorGuid);
        int ConnectedCount { get; }
    }
    
    public class VanillaBeltConveyorBlockInventoryInserter : IBeltConveyorBlockInventoryInserter
    {
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        private readonly BlockInstanceId _sourceBlockInstanceId;
        private int _roundRobinIndex = -1;

        public VanillaBeltConveyorBlockInventoryInserter(BlockInstanceId sourceBlockInstanceId, BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            _sourceBlockInstanceId = sourceBlockInstanceId;
            _blockConnectorComponent = blockConnectorComponent;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            var targets = _blockConnectorComponent.ConnectedTargets;
            if (targets.Count == 0) return itemStack;

            // ラウンドロビンで出力先を選択する
            // Select output target with round robin
            var connector = GetNextTarget(targets);

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
        public BlockConnectInfoElement GetNextGoalConnector()
        {
            var targets = _blockConnectorComponent.ConnectedTargets;
            if (targets.Count == 0) return null;

            // ラウンドロビンでGoalConnectorを選択する
            // Select goal connector with round robin
            return GetNextTarget(targets).Value.SelfConnector;
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
        /// GuidからGoalConnectorを取得
        /// Get GoalConnector by Guid
        /// </summary>
        public BlockConnectInfoElement GetGoalConnector(Guid connectorGuid)
        {
            foreach (var target in _blockConnectorComponent.ConnectedTargets)
            {
                if (target.Value.SelfConnector?.ConnectorGuid == connectorGuid) return target.Value.SelfConnector;
            }
            return null;
        }

        /// <summary>
        /// 接続されているコネクターの数を取得
        /// Get the number of connected connectors
        /// </summary>
        public int ConnectedCount => _blockConnectorComponent.ConnectedTargets.Count;


        private KeyValuePair<IBlockInventory, ConnectedInfo> GetNextTarget(IReadOnlyDictionary<IBlockInventory, ConnectedInfo> targets)
        {
            var targetsList = targets.ToArray();
            if (targetsList.Length == 0) return default;

            // 次の接続先インデックスを計算する
            // Calculate next target index
            _roundRobinIndex++;
            if (_roundRobinIndex >= targetsList.Length) _roundRobinIndex = 0;
            return targetsList[_roundRobinIndex];
        }
    }
}
