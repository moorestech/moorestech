using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Inventory;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;

namespace PlayerInventory
{
    public class BlockInventoryOpenStateDataStore : IBlockInventoryOpenStateDataStore
    {
        private readonly IWorldBlockComponentDatastore<IOpenableInventory> _worldBlockComponent;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        
        //key playerId, value block entity id
        private readonly Dictionary<int, int> _openCoordinates = new();

        public BlockInventoryOpenStateDataStore(
            IWorldBlockComponentDatastore<IOpenableInventory> worldBlockComponent, IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockComponent = worldBlockComponent;
            _worldBlockDatastore = worldBlockDatastore;
        }

        public List<int> GetBlockInventoryOpenPlayers(int blockEntityId)
        {
            return _openCoordinates.
                Where(x => x.Value == blockEntityId).
                Select(x => x.Key).ToList();
        }

        public void Open(int playerId, int x,int y)
        {
            //開けるインベントリのブロックが存在していなかったらそのまま終了
            if (!_worldBlockComponent.ExistsComponentBlock(x,y))return;

            var entityId = _worldBlockDatastore.GetBlock(x, y).GetEntityId();
            if (_openCoordinates.ContainsKey(playerId))
            {
                 _openCoordinates[playerId] = entityId;
            }
            else
            {
                _openCoordinates.Add(playerId, entityId);
            }
        }

        public void Close(int playerId)
        {
            if (_openCoordinates.ContainsKey(playerId))
            {
                _openCoordinates.Remove(playerId);
            }
        }
    }
}