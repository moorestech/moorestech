using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Connector;
using Game.Block.Blocks.Service;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlockConnectInfoModule;

namespace Game.Block.Blocks.BeltConveyor
{
    public interface IBeltConveyorBlockInventoryInserter : IBlockInventoryInserter
    {
        IItemStack InsertItem(IItemStack itemStack, BlockConnectInfoElement goalConnector);
        BlockConnectInfoElement PeekNextGoalConnector(List<IItemStack> itemStacks);
        BlockConnectInfoElement GetNextGoalConnector(List<IItemStack> itemStacks);
        bool IsValidGoalConnector(BlockConnectInfoElement goalConnector);
        int ConnectedCount { get; }
        bool HasAnyConnector { get; }
    }
    
    public class VanillaBeltConveyorBlockInventoryInserter : IBeltConveyorBlockInventoryInserter
    {
        private readonly InventoryConnectorTargetCache _targetCache;
        private readonly BlockInstanceId _sourceBlockInstanceId;
        private int _roundRobinIndex = -1;

        public VanillaBeltConveyorBlockInventoryInserter(BlockInstanceId sourceBlockInstanceId, BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            _sourceBlockInstanceId = sourceBlockInstanceId;
            _targetCache = new InventoryConnectorTargetCache(blockConnectorComponent);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            var targets = _targetCache.GetTargets();
            if (targets.Length == 0) return itemStack;

            var connector = GetNextTarget(targets);
            var context = new InsertItemContext(_sourceBlockInstanceId, connector.Value.SelfConnector, connector.Value.TargetConnector);
            return connector.Key.InsertItem(itemStack, context);
        }

        /// <summary>
        /// 特定のGoalConnectorを指定して出力
        /// Insert item to specific goal connector
        /// </summary>
        public IItemStack InsertItem(IItemStack itemStack, BlockConnectInfoElement goalConnector)
        {
            var targets = _targetCache.GetTargets();
            if (targets.Length == 0) return itemStack;

            // まず指定先に挿入し、失敗時は他の接続先を順に試す
            // Try the goal connector first, then attempt other targets on failure
            return TryInsertWithReselect(itemStack, goalConnector, targets);

            #region Internal

            IItemStack TryInsertWithReselect(IItemStack targetItem, BlockConnectInfoElement targetConnector, KeyValuePair<IBlockInventory, ConnectedInfo>[] connectedTargets)
            {
                var result = TryInsertToGoal(targetItem, targetConnector, connectedTargets, out var attemptedGoal);
                if (result.Id == ItemMaster.EmptyItemId) return result;

                // ゴール指定済みなら他の接続先だけを順に試す
                // When goal is set, try other targets in sequence
                for (var i = 0; i < connectedTargets.Length && result.Id != ItemMaster.EmptyItemId; i++)
                {
                    var nextTarget = GetNextTarget(connectedTargets);
                    if (attemptedGoal && targetConnector != null && nextTarget.Value.SelfConnector.ConnectorGuid == targetConnector.ConnectorGuid) continue;
                    result = InsertToTarget(result, nextTarget);
                }
                return result;
            }

            IItemStack TryInsertToGoal(IItemStack targetItem, BlockConnectInfoElement targetConnector, KeyValuePair<IBlockInventory, ConnectedInfo>[] connectedTargets, out bool attemptedGoal)
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
        /// 挿入可能なGoalConnectorを取得（インデックスを進めない）
        /// Get insertable goal connector (without advancing index)
        /// </summary>
        public BlockConnectInfoElement PeekNextGoalConnector(List<IItemStack> itemStacks)
        {
            var targets = _targetCache.GetTargets();
            if (targets.Length == 0) return null;

            // 挿入可能な接続先を探す（インデックスを進めない）
            // Find insertable connector (without advancing index)
            for (var i = 0; i < targets.Length; i++)
            {
                var target = PeekNextTarget(targets, i);
                if (!target.Key.InsertionCheck(itemStacks)) continue;
                return target.Value.SelfConnector;
            }

            return null;
        }

        /// <summary>
        /// 挿入可能なGoalConnectorを取得（インデックスを進める）
        /// Get insertable goal connector (advances index)
        /// </summary>
        public BlockConnectInfoElement GetNextGoalConnector(List<IItemStack> itemStacks)
        {
            var targets = _targetCache.GetTargets();
            if (targets.Length == 0) return null;

            // 挿入可能な接続先をラウンドロビンで選択する（インデックスを進める）
            // Select insertable connector with round robin (advances index)
            for (var i = 0; i < targets.Length; i++)
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
        public bool IsValidGoalConnector(BlockConnectInfoElement goalConnector)
        {
            if (goalConnector == null) return false;
            
            foreach (var target in _targetCache.GetTargets())
            {
                if (target.Value.SelfConnector.ConnectorGuid == goalConnector.ConnectorGuid) return true;
            }
            return false;
        }

        public int ConnectedCount => _targetCache.Count;

        /// <summary>
        /// SelfConnectorが設定されている接続先があるか
        /// Check if any target has SelfConnector set
        /// </summary>
        public bool HasAnyConnector
        {
            get
            {
                foreach (var target in _targetCache.GetTargets())
                {
                    if (target.Value.SelfConnector != null) return true;
                }
                return false;
            }
        }
        private KeyValuePair<IBlockInventory, ConnectedInfo> GetNextTarget(KeyValuePair<IBlockInventory, ConnectedInfo>[] targets)
        {
            if (targets.Length == 0) return default;

            // 次の接続先インデックスを計算する
            // Calculate next target index
            _roundRobinIndex++;
            if (_roundRobinIndex >= targets.Length) _roundRobinIndex = 0;
            return targets[_roundRobinIndex];
        }

        private KeyValuePair<IBlockInventory, ConnectedInfo> PeekNextTarget(KeyValuePair<IBlockInventory, ConnectedInfo>[] targets, int offset)
        {
            if (targets.Length == 0) return default;

            // 現在のインデックス + オフセットのターゲットを取得する（インデックスは進めない）
            // Get target at current index + offset (without advancing index)
            var index = (_roundRobinIndex + 1 + offset) % targets.Length;
            if (index < 0) index += targets.Length;
            return targets[index];
        }
    }
}
