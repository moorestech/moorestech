using System;
using MessagePack;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     照会した清浄機自身の状態（清浄機の画面の「この清浄機」の行）。組み立ては種別ごとの static factory、読み出しは TryGetStatus に閉じる
    ///     The queried purifier's own state (the purifier screen's "this purifier" line); built only through per-kind static factories and read only through TryGetStatus
    /// </summary>
    [MessagePackObject]
    public class CleanRoomQueriedAirFilterMessagePack
    {
        [Key(0)] public CleanRoomQueriedAirFilterResponseKind Kind { get; }

        // 電力の割合と、装填中のフィルター1個の残量（どちらも0〜1）。AirFilter のときだけ書かれる
        // Power ratio and the loaded filter's remaining life (both 0 to 1); written only for AirFilter
        [Key(1)] public double PowerRatio { get; }
        [Key(2)] public double FilterRemainingRatio { get; }

        [SerializationConstructor]
        public CleanRoomQueriedAirFilterMessagePack(CleanRoomQueriedAirFilterResponseKind kind, double powerRatio, double filterRemainingRatio)
        {
            Kind = kind;
            PowerRatio = powerRatio;
            FilterRemainingRatio = filterRemainingRatio;
        }

        public static CleanRoomQueriedAirFilterMessagePack CreateNotAirFilter()
        {
            return new CleanRoomQueriedAirFilterMessagePack(CleanRoomQueriedAirFilterResponseKind.NotAirFilter, 0, 0);
        }

        public static CleanRoomQueriedAirFilterMessagePack CreateAirFilter(double powerRatio, double filterRemainingRatio)
        {
            return new CleanRoomQueriedAirFilterMessagePack(CleanRoomQueriedAirFilterResponseKind.AirFilter, powerRatio, filterRemainingRatio);
        }

        // 清浄機から引いた照会のときだけ true
        // True only for a query from a purifier
        public bool TryGetStatus(out double powerRatio, out double filterRemainingRatio)
        {
            var isAirFilter = Kind == CleanRoomQueriedAirFilterResponseKind.AirFilter;
            powerRatio = isAirFilter ? PowerRatio : default;
            filterRemainingRatio = isAirFilter ? FilterRemainingRatio : default;
            return isAirFilter;
        }

        // 外から届いた値が読めるか。未知の種別を弾く
        // Whether a value from outside is readable, rejecting unknown kinds
        public bool IsReadable()
        {
            return Enum.IsDefined(typeof(CleanRoomQueriedAirFilterResponseKind), Kind);
        }
    }

    public enum CleanRoomQueriedAirFilterResponseKind
    {
        NotAirFilter,
        AirFilter,
    }
}
