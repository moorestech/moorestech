using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Game.PlayerInventory.Interface
{
    public interface IBlockInventoryOpenStateDataStore
    {
        public List<int> GetBlockInventoryOpenPlayers(BlockInstanceId blockBlockInstanceId);
        public void Open(int playerId, Vector3Int pos);
        public void Close(int playerId);
    }
}