using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Ui.State.Train.Logic;
using Game.Block.Blocks.TrainRail;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 時刻表へ入れられる駅一覧の唯一の組み立て。駅ブロックから読み、x → z → y で並べる（UITK の TrainTimetableModel と Web ホストの TrainTimetableDtoBuilder が呼ぶ。DC-23 A）
    /// The single builder of the stations a timetable can add: read from station blocks and ordered by x, then z, then y (called by the UITK TrainTimetableModel and the Web host TrainTimetableDtoBuilder; DC-23 A)
    /// </summary>
    public static class TrainTimetableStationCatalog
    {
        // 駅ブロックの位置と駅名（サーバーの StateDetail）を並べて返す
        // Returns station block positions and names (a server StateDetail), sorted
        public static List<TrainTimetableStation> ReadSorted(BlockGameObjectDataStore blocks)
        {
            var stations = new List<TrainTimetableStation>();
            foreach (var block in blocks.GetBlocksByBlockType(BlockTypeConst.TrainStation))
            {
                var name = block.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey)?.StationName;
                // silent-ok: 駅名の StateDetail が届く前は空名で並べ、無名の駅の文言で出す / 届かないままなら駅一覧が無名のままで、録画 train/train-timetable-via-ui の「1駅目は北駅」の主張が落ちて開発者が気づく
                stations.Add(new TrainTimetableStation(block.BlockPosInfo.OriginalPos, name ?? string.Empty));
            }
            return Sorted(stations);
        }

        public static List<TrainTimetableStation> Sorted(IEnumerable<TrainTimetableStation> stations)
        {
            var sorted = new List<TrainTimetableStation>(stations);
            sorted.Sort((a, b) =>
            {
                var byX = a.Position.x.CompareTo(b.Position.x);
                if (byX != 0) return byX;
                var byZ = a.Position.z.CompareTo(b.Position.z);
                return byZ != 0 ? byZ : a.Position.y.CompareTo(b.Position.y);
            });
            return sorted;
        }
    }
}
