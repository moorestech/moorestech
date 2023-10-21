using System.Collections.Generic;
using System.Linq;
using Game.Block.BlockInventory;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.World.EventHandler.InventoryEvent
{
    /// <summary>
    ///     
    /// </summary>
    public class BlockPlaceEventToBlockInventoryConnect
    {
        private readonly IBlockConfig _blockConfig;
        private readonly Dictionary<string, IoConnectionData> _ioConnectionDataDictionary = VanillaBlockInventoryConnectionData.IoConnectionData;
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public BlockPlaceEventToBlockInventoryConnect(IBlockPlaceEvent blockPlaceEvent, IBlockConfig blockConfig, IWorldBlockDatastore worldBlockDatastore)
        {
            _blockConfig = blockConfig;
            _worldBlockDatastore = worldBlockDatastore;
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }


        ///     

        /// <param name="blockPlaceEvent"></param>
        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEvent)
        {
            var connectOffsetBlockPositions = new List<(int, int)> { (1, 0), (-1, 0), (0, 1), (0, -1) };
            var x = blockPlaceEvent.Coordinate.X;
            var y = blockPlaceEvent.Coordinate.Y;

            foreach (var (offsetX, offsetY) in connectOffsetBlockPositions)
            {
                
                ConnectBlock(x, y, x + offsetX, y + offsetY);
                ConnectBlock(x + offsetX, y + offsetY, x, y);
            }
        }



        ///     
        ///     
        ///     ioConnectionDataDictionary
        ///     

        private void ConnectBlock(int sourceX, int sourceY, int destinationX, int destinationY)
        {
            //BlockInventory
            if (!_worldBlockDatastore.ExistsComponentBlock<IBlockInventory>(sourceX, sourceY) ||
                !_worldBlockDatastore.ExistsComponentBlock<IBlockInventory>(destinationX, destinationY)) return;


            
            var sourceBlock = _worldBlockDatastore.GetBlock(sourceX, sourceY);
            var sourceBlockType = _blockConfig.GetBlockConfig(sourceBlock.BlockId).Type;
            //Dictionary
            if (!_ioConnectionDataDictionary.ContainsKey(sourceBlockType)) return;

            var (_, sourceBlockOutputConnector) =
                GetConnectionPositions(
                    sourceBlockType,
                    _worldBlockDatastore.GetBlockDirection(sourceX, sourceY));


            
            var destinationBlock = _worldBlockDatastore.GetBlock(destinationX, destinationY);
            var destinationBlockType = _blockConfig.GetBlockConfig(destinationBlock.BlockId).Type;
            //Dictionary
            if (!_ioConnectionDataDictionary.ContainsKey(destinationBlockType)) return;

            var (destinationBlockInputConnector, _) =
                GetConnectionPositions(
                    destinationBlockType,
                    _worldBlockDatastore.GetBlockDirection(destinationX, destinationY));


            
            if (!_ioConnectionDataDictionary[sourceBlockType].ConnectableBlockType.Contains(destinationBlockType)) return;


            
            var distanceX = destinationX - sourceX;
            var distanceY = destinationY - sourceY;

            
            if (!sourceBlockOutputConnector.Contains(new ConnectDirection(distanceY, distanceX))) return;
            
            if (!destinationBlockInputConnector.Contains(new ConnectDirection(-distanceY, -distanceX))) return;


            
            _worldBlockDatastore.GetBlock<IBlockInventory>(sourceX, sourceY).AddOutputConnector(
                _worldBlockDatastore.GetBlock<IBlockInventory>(destinationX, destinationY));
        }


        ///     

        /// <param name="blockType"></param>
        /// <param name="blockDirection"></param>
        /// <returns></returns>
        private (List<ConnectDirection>, List<ConnectDirection>) GetConnectionPositions(string blockType,
            BlockDirection blockDirection)
        {
            var rawInputConnector = _ioConnectionDataDictionary[blockType].InputConnector;
            var rawOutputConnector = _ioConnectionDataDictionary[blockType].OutputConnector;
            var inputConnectionPositions = new List<ConnectDirection>();
            var outputConnectionPositions = new List<ConnectDirection>();

            
            switch (blockDirection)
            {
                case BlockDirection.North:
                    inputConnectionPositions = rawInputConnector.ToList();
                    outputConnectionPositions = rawOutputConnector.ToList();
                    break;
                case BlockDirection.East:
                    inputConnectionPositions =
                        rawInputConnector.Select(p => new ConnectDirection(-p.East, p.North)).ToList();
                    outputConnectionPositions = rawOutputConnector.Select(p => new ConnectDirection(-p.East, p.North))
                        .ToList();
                    break;
                case BlockDirection.South:
                    inputConnectionPositions = rawInputConnector.Select(p => new ConnectDirection(-p.North, -p.East))
                        .ToList();
                    outputConnectionPositions =
                        rawOutputConnector.Select(p => new ConnectDirection(-p.North, -p.East)).ToList();
                    break;
                case BlockDirection.West:
                    inputConnectionPositions =
                        rawInputConnector.Select(p => new ConnectDirection(p.East, -p.North)).ToList();
                    outputConnectionPositions = rawOutputConnector.Select(p => new ConnectDirection(p.East, -p.North))
                        .ToList();
                    break;
            }

            return (inputConnectionPositions, outputConnectionPositions);
        }
    }
}