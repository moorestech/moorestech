using System.Linq;
using Core.Item.Interface;
using Game.Block.Blocks.Connector;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.Context;
using Game.CraftChainer.BlockComponent.Computer;
using Game.CraftChainer.CraftNetwork;
using UnityEngine;

namespace Game.CraftChainer.BlockComponent
{
    /// <summary>
    /// そのアイテムがどのクラフトノードに挿入されるべきかを判断し、挿入するためのクラス
    /// Class for determining which craft node the item should be inserted into and inserting it
    /// </summary>
    public class ChainerTransporterInserter : IBlockInventoryInserter
    {
        public static CraftChainerNodeId Transporter_Test_NodeId;
        
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        private readonly CraftChainerNodeId _startChainerNodeId;
        
        public ChainerTransporterInserter(BlockConnectorComponent<IBlockInventory> blockConnectorComponent, CraftChainerNodeId startChainerNodeId)
        {
            _blockConnectorComponent = blockConnectorComponent;
            _startChainerNodeId = startChainerNodeId;
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            
            var context = CraftChainerManager.Instance.GetChainerNetworkContext(_startChainerNodeId);
            if (context == null)
            {
                return itemStack;
            }
            
            var target = context.GetTransportNextBlock(itemStack, _startChainerNodeId, _blockConnectorComponent);
            if (target == null) return itemStack;
            
            
            // DEBUG 消す
            var route = string.Empty;
            foreach (var result in ChainerNetworkContext.Result)
            {
                route += $"{result.Item1} -> ";
            }
            var start = TestDebug.TestStartTime;
            var end = System.DateTime.Now;
            var time = end - start;
            var seconds = time.TotalSeconds;
            
            var fromBlock = ServerContext.WorldBlockDatastore.GetBlock(_blockConnectorComponent);
            var fromPos = fromBlock.BlockPositionInfo.OriginalPos;
            var fromName = fromBlock.BlockMasterElement.Name;
            
            var toBlock = ServerContext.WorldBlockDatastore.GetBlock(target);
            var toPos = toBlock.BlockPositionInfo.OriginalPos;
            var toName = toBlock.BlockMasterElement.Name;
            
            //Debug.Log($"insert {itemStack}  {fromName} {fromPos} -> {toName} {toPos} Current: {_startChainerNodeId} route: {route} sec:{seconds:F2}");
            
            return target.InsertItem(itemStack);
        }
    }
}