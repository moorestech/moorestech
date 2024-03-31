using System.Collections.Generic;
using System.Linq;
using Server.Core.Inventory;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.PlayerInventory
{
    public class BlockInventoryOpenStateDataStore : IBlockInventoryOpenStateDataStore
    {
        //key playerId, value block entity id
        private readonly Dictionary<int, int> _openCoordinates = new();
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public BlockInventoryOpenStateDataStore(IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;
        }

        public List<int> GetBlockInventoryOpenPlayers(int blockEntityId)
        {
            return _openCoordinates.Where(x => x.Value == blockEntityId).Select(x => x.Key).ToList();
        }

        public void Open(int playerId, Vector3Int pos)
        {
            //開けるインベントリのブロックが存在していなかったらそのまま終了
            if (!_worldBlockDatastore.TryGetBlock<IOpenableInventory>(pos, out _)) return;

            var entityId = _worldBlockDatastore.GetBlock(pos).EntityId;
            _openCoordinates[playerId] = entityId;
        }

        public void Close(int playerId)
        {
            if (_openCoordinates.ContainsKey(playerId)) _openCoordinates.Remove(playerId);
        }
    }
}