using System;
using System.Collections.Generic;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     部屋1つの要約。組み立ては種別ごとの static factory、クラスの読み出しは TryGetClass に閉じる
    ///     Summary of one room; built only through per-kind static factories and read only through TryGetClass
    /// </summary>
    [MessagePackObject]
    public class CleanRoomSummaryMessagePack
    {
        [Key(0)] public CleanRoomClassResponseKind ClassKind { get; set; }

        // ClassName と ThresholdIndex は Classified のときだけ書かれる。読むのは TryGetClass だけ
        // ClassName and ThresholdIndex are written only when Classified; only TryGetClass reads them
        [Key(1)] public string ClassName { get; set; }
        [Key(2)] public int ThresholdIndex { get; set; }
        [Key(3)] public double Concentration { get; set; }
        [Key(4)] public double AirChangeRate { get; set; }
        [Key(5)] public int Volume { get; set; }
        [Key(6)] public List<CleanRoomAirFilterMessagePack> AirFilters { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public CleanRoomSummaryMessagePack() { }

        private CleanRoomSummaryMessagePack(CleanRoomClassResponseKind classKind, double concentration, double airChangeRate, int volume, List<CleanRoomAirFilterMessagePack> airFilters)
        {
            ClassKind = classKind;
            Concentration = concentration;
            AirChangeRate = airChangeRate;
            Volume = volume;
            AirFilters = airFilters;
        }

        public static CleanRoomSummaryMessagePack CreateClassified(string className, int thresholdIndex, double concentration, double airChangeRate, int volume, List<CleanRoomAirFilterMessagePack> airFilters)
        {
            return new CleanRoomSummaryMessagePack(CleanRoomClassResponseKind.Classified, concentration, airChangeRate, volume, airFilters) { ClassName = className, ThresholdIndex = thresholdIndex };
        }

        public static CleanRoomSummaryMessagePack CreateOut(double concentration, double airChangeRate, int volume, List<CleanRoomAirFilterMessagePack> airFilters)
        {
            return new CleanRoomSummaryMessagePack(CleanRoomClassResponseKind.Out, concentration, airChangeRate, volume, airFilters);
        }

        // Out なら false。判別子の読み分けはここだけで行う
        // False when Out; the only place that branches on the discriminator
        public bool TryGetClass(out string className, out int thresholdIndex)
        {
            switch (ClassKind)
            {
                case CleanRoomClassResponseKind.Classified:
                    className = ClassName;
                    thresholdIndex = ThresholdIndex;
                    return true;
                case CleanRoomClassResponseKind.Out:
                    className = default;
                    thresholdIndex = default;
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(ClassKind), ClassKind, "未対応のクラス種別 / Unsupported class kind");
            }
        }
    }

    [MessagePackObject]
    public class CleanRoomAirFilterMessagePack
    {
        [Key(0)] public Vector3IntMessagePack Position { get; set; }
        [Key(1)] public double PowerRatio { get; set; }
        [Key(2)] public double RemovalVolumePerSecond { get; set; }
        [Key(3)] public int FilterItemCount { get; set; }
        [Key(4)] public double FilterRemainingRatio { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public CleanRoomAirFilterMessagePack() { }

        public CleanRoomAirFilterMessagePack(Vector3IntMessagePack position, double powerRatio, double removalVolumePerSecond, int filterItemCount, double filterRemainingRatio)
        {
            Position = position;
            PowerRatio = powerRatio;
            RemovalVolumePerSecond = removalVolumePerSecond;
            FilterItemCount = filterItemCount;
            FilterRemainingRatio = filterRemainingRatio;
        }
    }
}
