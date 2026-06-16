using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Game.World.DataStore
{
    public class BlockCoordinateIndex
    {
        private readonly Dictionary<Vector3Int, BlockInstanceId> _coordinateToBlockInstanceId = new();

        public bool TryGetBlockInstanceId(Vector3Int pos, out BlockInstanceId blockInstanceId)
        {
            return _coordinateToBlockInstanceId.TryGetValue(pos, out blockInstanceId);
        }

        public bool ContainsAny(BlockPositionInfo positionInfo)
        {
            // 配置予定の占有座標だけを調べ、全ブロック走査を避ける
            // Check only the target footprint to avoid scanning all blocks.
            foreach (var pos in EnumeratePositions(positionInfo))
                if (_coordinateToBlockInstanceId.ContainsKey(pos))
                    return true;

            return false;
        }

        public void Add(BlockPositionInfo positionInfo, BlockInstanceId blockInstanceId)
        {
            // 複数マスブロックも全占有座標から元ブロックを引けるようにする
            // Index every occupied cell so multi-cell blocks resolve from any position.
            foreach (var pos in EnumeratePositions(positionInfo))
                _coordinateToBlockInstanceId.Add(pos, blockInstanceId);
        }

        public void Remove(BlockPositionInfo positionInfo)
        {
            foreach (var pos in EnumeratePositions(positionInfo))
                _coordinateToBlockInstanceId.Remove(pos);
        }

        public static IEnumerable<Vector3Int> EnumeratePositions(BlockPositionInfo positionInfo)
        {
            // BlockPositionInfo の min/max を唯一の footprint 展開元にする
            // Use BlockPositionInfo min/max as the single footprint expansion source.
            for (var x = positionInfo.MinPos.x; x <= positionInfo.MaxPos.x; x++)
            for (var y = positionInfo.MinPos.y; y <= positionInfo.MaxPos.y; y++)
            for (var z = positionInfo.MinPos.z; z <= positionInfo.MaxPos.z; z++)
                yield return new Vector3Int(x, y, z);
        }
    }
}
