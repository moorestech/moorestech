using Game.Block.Component.IOConnector;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface;
using Game.World.Interface.DataStore;

namespace Game.Block.Component
{
    public class ComponentFactory
    {
        private readonly IBlockConfig _blockConfig;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IWorldBlockUpdateEvent _worldBlockUpdateEvent;

        public ComponentFactory(IWorldBlockDatastore worldBlockDatastore, IBlockConfig blockConfig, IWorldBlockUpdateEvent worldBlockUpdateEvent)
        {
            _worldBlockDatastore = worldBlockDatastore;
            _blockConfig = blockConfig;
            _worldBlockUpdateEvent = worldBlockUpdateEvent;
        }

        public InputConnectorComponent CreateInputConnectorComponent(BlockPositionInfo blockPositionInfo, IOConnectionSetting ioConnectionSetting)
        {
            return new InputConnectorComponent(_worldBlockDatastore, _blockConfig, _worldBlockUpdateEvent, ioConnectionSetting, blockPositionInfo);
        }
    }
}