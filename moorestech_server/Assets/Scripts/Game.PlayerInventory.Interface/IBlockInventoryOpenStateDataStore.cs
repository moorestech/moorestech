using System.Collections.Generic;
using UnityEngine;

namespace Game.PlayerInventory.Interface
{
    public interface IBlockInventoryOpenStateDataStore
    {
        public List<int> GetBlockInventoryOpenPlayers(int blockEntityId);
        public void Open(int playerId, Vector2Int pos);
        public void Close(int playerId);
    }
}