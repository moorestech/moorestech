using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom.Info.Leak;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom.Info
{
    /// <summary>
    ///     ブロック1つから見た部屋の状態を組み立てる（UI・ツールチップ用の読み取り専用照会）
    ///     Builds the room state seen from one block, a read-only query for UI and tooltips
    /// </summary>
    public class CleanRoomInfoQuery
    {
        private static readonly Vector3Int[] TwentySixOffsets = BuildTwentySixOffsets();

        private readonly CleanRoomDatastore _datastore;
        private readonly IWorldBlockDatastore _world;

        public CleanRoomInfoQuery(CleanRoomDatastore datastore, IWorldBlockDatastore world)
        {
            _datastore = datastore;
            _world = world;
        }

        public CleanRoomInfo Query(IBlock block)
        {
            var cells = block.BlockPositionInfo.EnumeratePositions().ToList();
            var isBoundary = block.ExistsComponent<ICleanRoomBoundaryComponent>();

            // 境界は隣接する部屋、室内のブロックは自分が属する部屋を引く
            // A boundary resolves its adjacent rooms; an interior block resolves the room it belongs to
            var rooms = isBoundary ? CollectAdjacentRooms() : CollectOwnRoom();
            if (rooms.Count > 0) return new CleanRoomEnclosedInfo(rooms.Select(room => CleanRoomRoomSummary.Create(room, _world)).ToList());

            // 部屋が無ければ漏れ箇所を探して返す
            // Without a room, probe for the openings
            var leak = isBoundary ? CleanRoomLeakProbe.ProbeFromBoundary(_world, cells) : CleanRoomLeakProbe.ProbeFromSeeds(_world, cells);
            return new CleanRoomNotEnclosedInfo(leak);

            #region Internal

            List<CleanRoom> CollectAdjacentRooms()
            {
                // 角の壁は6近傍に部屋セルを持たないため26近傍で探す
                // Corner walls have no room cell among six neighbors, so search the 26 neighbors
                var found = new List<CleanRoom>();
                foreach (var cell in cells)
                foreach (var offset in TwentySixOffsets)
                {
                    if (!_datastore.TryGetCleanRoomAt(cell + offset, out var room) || found.Contains(room)) continue;
                    found.Add(room);
                }

                return found;
            }

            List<CleanRoom> CollectOwnRoom()
            {
                return _datastore.TryGetCleanRoom(block, out var room) ? new List<CleanRoom> { room } : new List<CleanRoom>();
            }

            #endregion
        }

        private static Vector3Int[] BuildTwentySixOffsets()
        {
            var offsets = new List<Vector3Int>();
            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
            for (var z = -1; z <= 1; z++)
            {
                if (x == 0 && y == 0 && z == 0) continue;
                offsets.Add(new Vector3Int(x, y, z));
            }

            return offsets.ToArray();
        }
    }
}
