using System;
using Game.Block.Interface.Component;
using MessagePack;

namespace Game.Block.Blocks.TrainRail
{
    // 駅名をブロック状態としてクライアントへ伝える
    // Carry the station name to clients through block state
    [MessagePackObject]
    public class TrainStationNameStateDetail
    {
        public const string BlockStateDetailKey = "TrainStationName";

        [Key(0)] public string StationName { get; set; }

        public TrainStationNameStateDetail(string stationName)
        {
            StationName = stationName;
        }

        public static BlockStateDetail CreateState(string stationName)
        {
            var detail = new TrainStationNameStateDetail(stationName);
            return new BlockStateDetail(BlockStateDetailKey, MessagePackSerializer.Serialize(detail));
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public TrainStationNameStateDetail() { }
    }
}
