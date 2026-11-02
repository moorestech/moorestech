using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.CleanRoom.Info.Leak;
using Game.World.Interface.DataStore;

namespace Game.CleanRoom.Info
{
    /// <summary>
    ///     室内のブロック1つから見た部屋の状態を組み立てる（UI用の読み取り専用照会）
    ///     Builds the room state seen from one interior block, a read-only query for UI
    /// </summary>
    public class CleanRoomInfoQuery
    {
        private readonly CleanRoomDatastore _datastore;
        private readonly IWorldBlockDatastore _world;

        public CleanRoomInfoQuery(CleanRoomDatastore datastore, IWorldBlockDatastore world)
        {
            _datastore = datastore;
            _world = world;
        }

        public CleanRoomInfo Query(IBlock block)
        {
            // 自分が属する部屋を引く
            // Resolve the room the block belongs to
            if (_datastore.TryGetCleanRoom(block, out var room)) return new CleanRoomEnclosedInfo(CleanRoomRoomSummary.Create(room, _world));

            // 部屋が無ければ漏れ箇所を探して返す
            // Without a room, probe for the openings
            var cells = block.BlockPositionInfo.EnumeratePositions().ToList();
            return new CleanRoomNotEnclosedInfo(CleanRoomLeakProbe.ProbeFromSeeds(_world, cells));
        }
    }
}
