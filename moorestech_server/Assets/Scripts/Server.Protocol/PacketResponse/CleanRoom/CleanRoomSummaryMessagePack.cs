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
        [Key(0)] public CleanRoomClassResponseKind ClassKind { get; }

        // ClassName と ThresholdIndex は Classified のときだけ書かれる。読むのは TryGetClass だけ
        // ClassName and ThresholdIndex are written only when Classified; only TryGetClass reads them
        [Key(1)] public string ClassName { get; }
        [Key(2)] public int ThresholdIndex { get; }
        [Key(3)] public double AirChangeRate { get; }
        [Key(4)] public int Volume { get; }
        [Key(5)] public List<CleanRoomAirFilterMessagePack> AirFilters { get; }

        // 清浄機の台数のまとめと、次のクラスまでの台数（清浄機の画面の部屋の行）
        // Purifier totals and units to the next class (the room line of the purifier screen)
        [Key(6)] public int AirFilterCount { get; }
        [Key(7)] public int StoppedAirFilterCount { get; }
        [Key(8)] public CleanRoomNextClassMessagePack NextClass { get; }

        // 照会した清浄機自身の状態（清浄機の画面の「この清浄機」の行）
        // The queried purifier's own state (the purifier screen's "this purifier" line)
        [Key(9)] public CleanRoomQueriedAirFilterMessagePack QueriedAirFilter { get; }

        [SerializationConstructor]
        public CleanRoomSummaryMessagePack(CleanRoomClassResponseKind classKind, string className, int thresholdIndex, double airChangeRate, int volume, List<CleanRoomAirFilterMessagePack> airFilters, int airFilterCount, int stoppedAirFilterCount, CleanRoomNextClassMessagePack nextClass, CleanRoomQueriedAirFilterMessagePack queriedAirFilter)
        {
            ClassKind = classKind;
            ClassName = className;
            ThresholdIndex = thresholdIndex;
            AirChangeRate = airChangeRate;
            Volume = volume;
            AirFilters = airFilters;
            AirFilterCount = airFilterCount;
            StoppedAirFilterCount = stoppedAirFilterCount;
            NextClass = nextClass;
            QueriedAirFilter = queriedAirFilter;
        }

        public static CleanRoomSummaryMessagePack CreateClassified(string className, int thresholdIndex, double airChangeRate, int volume, List<CleanRoomAirFilterMessagePack> airFilters, int airFilterCount, int stoppedAirFilterCount, CleanRoomNextClassMessagePack nextClass, CleanRoomQueriedAirFilterMessagePack queriedAirFilter)
        {
            return new CleanRoomSummaryMessagePack(CleanRoomClassResponseKind.Classified, className, thresholdIndex, airChangeRate, volume, airFilters, airFilterCount, stoppedAirFilterCount, nextClass, queriedAirFilter);
        }

        public static CleanRoomSummaryMessagePack CreateOut(double airChangeRate, int volume, List<CleanRoomAirFilterMessagePack> airFilters, int airFilterCount, int stoppedAirFilterCount, CleanRoomNextClassMessagePack nextClass, CleanRoomQueriedAirFilterMessagePack queriedAirFilter)
        {
            return new CleanRoomSummaryMessagePack(CleanRoomClassResponseKind.Out, null, 0, airChangeRate, volume, airFilters, airFilterCount, stoppedAirFilterCount, nextClass, queriedAirFilter);
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
