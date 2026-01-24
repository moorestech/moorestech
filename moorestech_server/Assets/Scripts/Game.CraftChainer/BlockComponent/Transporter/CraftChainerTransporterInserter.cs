using System;
using System.Linq;
using Core.Item.Interface;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Connector;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.CraftChainer.CraftNetwork;
using Mooresmaster.Model.BlockConnectInfoModule;

namespace Game.CraftChainer.BlockComponent
{
    /// <summary>
    /// そのアイテムがどのクラフトノードに挿入されるべきかを判断し、挿入するためのクラス
    /// Class for determining which craft node the item should be inserted into and inserting it
    /// </summary>
    public class CraftChainerTransporterInserter : IBeltConveyorBlockInventoryInserter
    {
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        private readonly CraftChainerNodeId _startChainerNodeId;
        
        public CraftChainerTransporterInserter(BlockConnectorComponent<IBlockInventory> blockConnectorComponent, CraftChainerNodeId startChainerNodeId)
        {
            _blockConnectorComponent = blockConnectorComponent;
            _startChainerNodeId = startChainerNodeId;
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            var chainerContext = CraftChainerMainComputerManager.Instance.GetChainerNetworkContext(_startChainerNodeId);
            if (chainerContext == null)
            {
                return itemStack;
            }

            // transporterの場合は既に1個になっているアイテムを挿入する想定
            // In the case of a transporter, it is assumed that the item has already been reduced to one
            return chainerContext.InsertNodeNetworkNextBlock(itemStack, _startChainerNodeId, _blockConnectorComponent);
        }

        public IItemStack InsertItem(IItemStack itemStack, BlockConnectInfoElement goalConnector)
        {
            return InsertItem(itemStack);
        }

        public BlockConnectInfoElement GetNextGoalConnector()
        {
            var targets = _blockConnectorComponent.ConnectedTargets;
            if (targets.Count == 0) return null;
            return targets.First().Value.SelfConnector;
        }

        public BlockConnectInfoElement GetNextGoalConnector(System.Collections.Generic.List<IItemStack> itemStacks)
        {
            return GetNextGoalConnector();
        }

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
    }
}
