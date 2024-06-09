using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.PlayerInventory
{
    public class BlockInventoryOpenStateDataStore : IBlockInventoryOpenStateDataStore
    {
        //key playerId, value block entity id
        private readonly Dictionary<int, BlockInstanceId> _openCoordinates = new();
        
        public List<int> GetBlockInventoryOpenPlayers(BlockInstanceId blockBlockInstanceId)
        {
            return _openCoordinates.Where(x => x.Value == blockBlockInstanceId).Select(x => x.Key).ToList();
        }
        
        public void Open(int playerId, Vector3Int pos)
        {
            //開けるインベントリのブロックが存在していなかったらそのまま終了
            if (!ServerContext.WorldBlockDatastore.TryGetBlock<IOpenableBlockInventoryComponent>(pos, out _)) return;
            
            var entityId = ServerContext.WorldBlockDatastore.GetBlock(pos).BlockInstanceId;
            _openCoordinates[playerId] = entityId;
        }
        
        public void Close(int playerId)
        {
            if (_openCoordinates.ContainsKey(playerId)) _openCoordinates.Remove(playerId);
        }
    }
}