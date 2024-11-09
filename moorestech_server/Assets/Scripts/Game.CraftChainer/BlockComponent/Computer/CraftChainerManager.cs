using System.Collections.Generic;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.CraftChainer.BlockComponent.Computer;
using Game.World.Interface.DataStore;
using UniRx;

namespace Game.CraftChainer.CraftNetwork
{
    public class CraftChainerManager
    {
        public static CraftChainerManager Instance;
        
        private readonly List<ChainerMainComputerComponent> _mainComputers = new();
        
        public CraftChainerManager()
        {
            Instance = this;
            
            ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlaceEvent);
            ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemoveEvent);
        }
        
        private void OnBlockPlaceEvent(BlockUpdateProperties updateProperties)
        {
            var block = updateProperties.BlockData.Block;
            if (block.TryGetComponent<ChainerMainComputerComponent>(out var mainComputer))
            {
                _mainComputers.Add(mainComputer);
            }
            if (block.ExistsComponent<ICraftChainerNode>())
            {
                foreach (var computer in _mainComputers)
                {
                    computer.ChainerNetworkContext.ReSearchNetwork();
                }
            }
        }
        
        private void OnBlockRemoveEvent(BlockUpdateProperties updateProperties)
        {
            var block = updateProperties.BlockData.Block;
            if (block.TryGetComponent<ChainerMainComputerComponent>(out var mainComputer))
            {
                _mainComputers.Remove(mainComputer);
            }
            if (block.ExistsComponent<ICraftChainerNode>())
            {
                foreach (var computer in _mainComputers)
                {
                    computer.ChainerNetworkContext.ReSearchNetwork();
                }
            }
        }
        
        
        public ChainerNetworkContext GetChainerNetworkContext(CraftChainerNodeId nodeId)
        {
            foreach (var mainComputer in _mainComputers)
            {
                if (mainComputer.ChainerNetworkContext.IsExistNode(nodeId))
                {
                    return mainComputer.ChainerNetworkContext;
                }
            }
            
            return null;
        }
    }
}