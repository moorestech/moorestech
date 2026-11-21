using System;
using MessagePack;

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

        // Key 3・4・5 は外した項目（換気回数・体積・清浄機の一覧）の欠番
        // Keys 3, 4 and 5 are retired gaps (air change rate, volume, purifier list)
        // 清浄機の台数のまとめと、次のクラスまでの台数（清浄機の画面の部屋の行）
        // Purifier totals and units to the next class (the room line of the purifier screen)
        [Key(6)] public int AirFilterCount { get; }
        [Key(7)] public int StoppedAirFilterCount { get; }
        [Key(8)] public CleanRoomNextClassMessagePack NextClass { get; }

        // 照会した清浄機自身の状態（清浄機の画面の「この清浄機」の行）
        // The queried purifier's own state (the purifier screen's "this purifier" line)
        [Key(9)] public CleanRoomQueriedAirFilterMessagePack QueriedAirFilter { get; }

        [SerializationConstructor]
        public CleanRoomSummaryMessagePack(CleanRoomClassResponseKind classKind, string className, int thresholdIndex, int airFilterCount, int stoppedAirFilterCount, CleanRoomNextClassMessagePack nextClass, CleanRoomQueriedAirFilterMessagePack queriedAirFilter)
        {
            ClassKind = classKind;
            ClassName = className;
            ThresholdIndex = thresholdIndex;
            AirFilterCount = airFilterCount;
            StoppedAirFilterCount = stoppedAirFilterCount;
            NextClass = nextClass;
            QueriedAirFilter = queriedAirFilter;
        }

        public static CleanRoomSummaryMessagePack CreateClassified(string className, int thresholdIndex, int airFilterCount, int stoppedAirFilterCount, CleanRoomNextClassMessagePack nextClass, CleanRoomQueriedAirFilterMessagePack queriedAirFilter)
        {
            return new CleanRoomSummaryMessagePack(CleanRoomClassResponseKind.Classified, className, thresholdIndex, airFilterCount, stoppedAirFilterCount, nextClass, queriedAirFilter);
        }

        public static CleanRoomSummaryMessagePack CreateOut(int airFilterCount, int stoppedAirFilterCount, CleanRoomNextClassMessagePack nextClass, CleanRoomQueriedAirFilterMessagePack queriedAirFilter)
        {
            return new CleanRoomSummaryMessagePack(CleanRoomClassResponseKind.Out, null, 0, airFilterCount, stoppedAirFilterCount, nextClass, queriedAirFilter);
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
}
