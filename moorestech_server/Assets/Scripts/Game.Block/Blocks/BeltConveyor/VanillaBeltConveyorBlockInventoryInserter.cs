using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Connector;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.BeltConveyor
{
    public interface IBeltConveyorBlockInventoryInserter : IBlockInventoryInserter
    {
        IItemStack InsertItem(IItemStack itemStack, IBlockConnector goalConnector);
        IBlockConnector GetNextGoalConnector();
        IBlockConnector GetNextGoalConnector(List<IItemStack> itemStacks);
        bool IsValidGoalConnector(IBlockConnector goalConnector);
        int ConnectedCount { get; }
        bool HasAnyConnector { get; }
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

            // ConnectedInfoからIBlockConnectorを取得
            // Get IBlockConnector from ConnectedInfo
            var context = new InsertItemContext(_sourceBlockInstanceId, connector.Value.SelfConnector, connector.Value.TargetConnector);

            return connector.Key.InsertItem(itemStack, context);
        }

        /// <summary>
        /// 特定のGoalConnectorを指定して出力
        /// Insert item to specific goal connector
        /// </summary>
        public IItemStack InsertItem(IItemStack itemStack, IBlockConnector goalConnector)
        {
            var targets = _blockConnectorComponent.ConnectedTargets;
            if (targets.Count == 0) return itemStack;

            // まず指定先に挿入し、失敗時は他の接続先を順に試す
            // Try the goal connector first, then attempt other targets on failure
            return TryInsertWithReselect(itemStack, goalConnector, targets);

            #region Internal

            IItemStack TryInsertWithReselect(IItemStack targetItem, IBlockConnector targetConnector, IReadOnlyDictionary<IBlockInventory, ConnectedInfo> connectedTargets)
            {
                var result = TryInsertToGoal(targetItem, targetConnector, connectedTargets, out var attemptedGoal);
                if (result.Id == ItemMaster.EmptyItemId) return result;

                // ゴール指定済みなら他の接続先だけを順に試す
                // When goal is set, try other targets in sequence
                for (var i = 0; i < connectedTargets.Count && result.Id != ItemMaster.EmptyItemId; i++)
                {
                    var nextTarget = GetNextTarget(connectedTargets);
                    if (attemptedGoal && targetConnector != null && nextTarget.Value.SelfConnector.ConnectorGuid == targetConnector.ConnectorGuid) continue;
                    result = InsertToTarget(result, nextTarget);
                }

                return result;
            }

            IItemStack TryInsertToGoal(IItemStack targetItem, IBlockConnector targetConnector, IReadOnlyDictionary<IBlockInventory, ConnectedInfo> connectedTargets, out bool attemptedGoal)
            {
                attemptedGoal = false;
                if (targetConnector == null) return targetItem;

                foreach (var target in connectedTargets)
                {
                    if (target.Value.SelfConnector.ConnectorGuid != targetConnector.ConnectorGuid) continue;
                    attemptedGoal = true;
                    return InsertToTarget(targetItem, target);
                }

                return targetItem;
            }

            IItemStack InsertToTarget(IItemStack targetItem, KeyValuePair<IBlockInventory, ConnectedInfo> target)
            {
                var context = new InsertItemContext(_sourceBlockInstanceId, target.Value.SelfConnector, target.Value.TargetConnector);
                return target.Key.InsertItem(targetItem, context);
            }

            #endregion
        }

        /// <summary>
        /// 最初のGoalConnectorを取得
        /// Get first goal connector
        /// </summary>
        public IBlockConnector GetNextGoalConnector()
        {
            var targets = _blockConnectorComponent.ConnectedTargets;
            if (targets.Count == 0) return null;

            // ラウンドロビンでGoalConnectorを選択する
            // Select goal connector with round robin
            return GetNextTarget(targets).Value.SelfConnector;
        }

        /// <summary>
        /// 挿入可能なGoalConnectorを取得
        /// Get insertable goal connector
        /// </summary>
        public IBlockConnector GetNextGoalConnector(List<IItemStack> itemStacks)
        {
            var targets = _blockConnectorComponent.ConnectedTargets;
            if (targets.Count == 0) return null;

            // 挿入可能な接続先をラウンドロビンで選択する
            // Select insertable connector with round robin
            var targetsList = targets.ToArray();
            for (var i = 0; i < targetsList.Length; i++)
            {
                var target = GetNextTarget(targets);
                if (!target.Key.InsertionCheck(itemStacks)) continue;
                return target.Value.SelfConnector;
            }

            return null;
        }

        /// <summary>
        /// 指定されたGoalConnectorが有効かどうかを確認
        /// Check if specified GoalConnector is valid
        /// </summary>
        public bool IsValidGoalConnector(IBlockConnector goalConnector)
        {
            if (goalConnector == null) return false;

            foreach (var target in _blockConnectorComponent.ConnectedTargets)
            {
                if (target.Value.SelfConnector.ConnectorGuid == goalConnector.ConnectorGuid) return true;
            }

            return false;
        }

        /// <summary>
        /// 接続されているコネクターの数を取得
        /// Get the number of connected connectors
        /// </summary>
        public int ConnectedCount => _blockConnectorComponent.ConnectedTargets.Count;

        /// <summary>
        /// SelfConnectorが設定されている接続先があるか
        /// Check if any target has SelfConnector set
        /// </summary>
        public bool HasAnyConnector
        {
            get
            {
                foreach (var target in _blockConnectorComponent.ConnectedTargets)
                {
                    if (target.Value.SelfConnector != null) return true;
                }

                return false;
            }
        }


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
