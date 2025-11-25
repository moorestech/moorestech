using System.Collections.Generic;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.CraftChainer.BlockComponent.Computer;
using Game.World.Interface.DataStore;
using UniRx;

namespace Game.CraftChainer.CraftNetwork
{
    public class CraftChainerMainComputerManager
    {
        public static CraftChainerMainComputerManager Instance;
        
        private readonly List<CraftChainerMainComputerComponent> _mainComputers = new();
        
        public CraftChainerMainComputerManager()
        {
            Instance = this;
            
            ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlaceEvent);
            ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemoveEvent);
        }
        
        private void OnBlockPlaceEvent(BlockPlaceProperties updateProperties)
        {
            var block = updateProperties.BlockData.Block;
            if (block.TryGetComponent<CraftChainerMainComputerComponent>(out var mainComputer))
            {
                _mainComputers.Add(mainComputer);
            }
            if (block.ExistsComponent<ICraftChainerNode>())
            {
                foreach (var computer in _mainComputers)
                {
                    computer.CraftChainerNetworkContext.ReSearchNetwork();
                }
            }
        }
        
        private void OnBlockRemoveEvent(BlockRemoveProperties updateProperties)
        {
            var block = updateProperties.BlockData.Block;
            if (block.TryGetComponent<CraftChainerMainComputerComponent>(out var mainComputer))
            {
                _mainComputers.Remove(mainComputer);
            }
            if (block.ExistsComponent<ICraftChainerNode>())
            {
                foreach (var computer in _mainComputers)
                {
                    computer.CraftChainerNetworkContext.ReSearchNetwork();
                }
            }
        }
        
        
        public CraftChainerNetworkContext GetChainerNetworkContext(CraftChainerNodeId nodeId)
        {
            foreach (var mainComputer in _mainComputers)
            {
                if (mainComputer.CraftChainerNetworkContext.IsExistNode(nodeId))
                {
                    return mainComputer.CraftChainerNetworkContext;
                }
            }
            
            return null;
        }
    }
}