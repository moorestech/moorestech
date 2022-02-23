using System;
using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;

namespace PlayerInventory
{
    public class BlockInventoryOpenStateDataStore : IBlockInventoryOpenStateDataStore
    {
        private readonly Dictionary<int, Coordinate> _openCoordinates = new();
        public bool IsOpen(int playerId)
        {
            return _openCoordinates.ContainsKey(playerId);
        }

        public Coordinate GetOpenCoordinates(int playerId)
        {
            if (_openCoordinates.ContainsKey(playerId))
            {
                return _openCoordinates[playerId];
            }

            throw new Exception($"PlayerId : {playerId} はインベントリを開いていません");
        }

        public void Open(int playerId, Coordinate coordinate)
        {
            if (_openCoordinates.ContainsKey(playerId))
            {
                 _openCoordinates[playerId] = coordinate;
            }
            else
            {
                _openCoordinates.Add(playerId, coordinate);
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