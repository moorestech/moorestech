using Game.Block.Component.IOConnector;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface;
using Game.World.Interface.DataStore;

namespace Game.Block.Component
{
    public class ComponentFactory
    {
        //TODO この辺はコンフィグ類も含めてコンテキストに乗せるようにする
        public static ComponentFactory Instance { get; private set; }
        
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IBlockConfig _blockConfig;
        private readonly IWorldBlockUpdateEvent _worldBlockUpdateEvent;
        
        public ComponentFactory(IWorldBlockDatastore worldBlockDatastore, IBlockConfig blockConfig, IWorldBlockUpdateEvent worldBlockUpdateEvent)
        {
            _worldBlockDatastore = worldBlockDatastore;
            _blockConfig = blockConfig;
            _worldBlockUpdateEvent = worldBlockUpdateEvent;
            Instance = this;
        }
        
        public InputConnectorComponent CreateInputConnectorComponent(BlockPositionInfo blockPositionInfo,IOConnectionSetting ioConnectionSetting)
        {
            return new InputConnectorComponent(_worldBlockDatastore,_blockConfig,_worldBlockUpdateEvent,ioConnectionSetting,blockPositionInfo);
        }
    }
}