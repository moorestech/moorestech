using MessagePack;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     照会した清浄機の状態と次のクラスまでの台数。組み立ては種別ごとの static factory、読み出しは TryGet に閉じる
    ///     The queried purifier's state and units to the next class; built only through per-kind static factories and read only through the TryGet methods
    /// </summary>
    [MessagePackObject]
    public class CleanRoomQueriedAirFilterMessagePack
    {
        [Key(0)] public CleanRoomQueriedAirFilterResponseKind Kind { get; }

        // 電力の割合と、装填中のフィルター1個の残量（どちらも0〜1）。清浄機のときだけ書かれる
        // Power ratio and the loaded filter's remaining life (both 0 to 1); written only for a purifier
        [Key(1)] public double PowerRatio { get; }
        [Key(2)] public double FilterRemainingRatio { get; }

        // 次のクラスの名前と台数。AirFilterWithUnits のときだけ書かれる
        // Next class name and units; written only for AirFilterWithUnits
        [Key(3)] public string NextClassName { get; }
        [Key(4)] public int UnitsToNextClass { get; }

        [SerializationConstructor]
        public CleanRoomQueriedAirFilterMessagePack(CleanRoomQueriedAirFilterResponseKind kind, double powerRatio, double filterRemainingRatio, string nextClassName, int unitsToNextClass)
        {
            Kind = kind;
            PowerRatio = powerRatio;
            FilterRemainingRatio = filterRemainingRatio;
            NextClassName = nextClassName;
            UnitsToNextClass = unitsToNextClass;
        }

        public static CleanRoomQueriedAirFilterMessagePack CreateNotAirFilter()
        {
            return new CleanRoomQueriedAirFilterMessagePack(CleanRoomQueriedAirFilterResponseKind.NotAirFilter, 0, 0, null, 0);
        }

        public static CleanRoomQueriedAirFilterMessagePack CreateAirFilter(double powerRatio, double filterRemainingRatio)
        {
            return new CleanRoomQueriedAirFilterMessagePack(CleanRoomQueriedAirFilterResponseKind.AirFilter, powerRatio, filterRemainingRatio, null, 0);
        }

        public static CleanRoomQueriedAirFilterMessagePack CreateAirFilterWithUnits(double powerRatio, double filterRemainingRatio, string nextClassName, int unitsToNextClass)
        {
            return new CleanRoomQueriedAirFilterMessagePack(CleanRoomQueriedAirFilterResponseKind.AirFilterWithUnits, powerRatio, filterRemainingRatio, nextClassName, unitsToNextClass);
        }

        // 清浄機から引いた照会のときだけ true
        // True only for a query from a purifier
        public bool TryGetStatus(out double powerRatio, out double filterRemainingRatio)
        {
            var isAirFilter = Kind is CleanRoomQueriedAirFilterResponseKind.AirFilter or CleanRoomQueriedAirFilterResponseKind.AirFilterWithUnits;
            powerRatio = isAirFilter ? PowerRatio : default;
            filterRemainingRatio = isAirFilter ? FilterRemainingRatio : default;
            return isAirFilter;
        }

        // 「（クラスBまであと5台）」を出せるときだけ true
        // True only when "(5 more to class B)" can be shown
        public bool TryGetUnitsToNextClass(out string nextClassName, out int units)
        {
            var hasUnits = Kind == CleanRoomQueriedAirFilterResponseKind.AirFilterWithUnits;
            nextClassName = hasUnits ? NextClassName : default;
            units = hasUnits ? UnitsToNextClass : default;
            return hasUnits;
        }

        // 外から届いた値が読めるか。未知の種別と、台数の種別に要る値の欠けを弾く
        // Whether a value from outside is readable, rejecting unknown kinds and missing unit values
        public bool IsReadable()
        {
            return Kind switch
            {
                CleanRoomQueriedAirFilterResponseKind.NotAirFilter => true,
                CleanRoomQueriedAirFilterResponseKind.AirFilter => true,
                CleanRoomQueriedAirFilterResponseKind.AirFilterWithUnits => NextClassName != null && 0 < UnitsToNextClass,
                _ => false,
            };
        }
    }

    public enum CleanRoomQueriedAirFilterResponseKind
    {
        NotAirFilter,
        AirFilter,
        AirFilterWithUnits,
    }
}
