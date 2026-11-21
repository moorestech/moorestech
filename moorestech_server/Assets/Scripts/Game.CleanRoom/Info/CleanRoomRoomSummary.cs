using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom.Util;
using Game.World.Interface.DataStore;

namespace Game.CleanRoom.Info
{
    /// <summary>
    ///     部屋1つのクラス・清浄機の台数のまとめ・照会の出どころ（清浄機から引いたときだけ、その清浄機の状態と次のクラスまでの見通し）
    ///     Class, purifier totals and query source of one room (only from a purifier: that purifier's state and the outlook to the next class)
    /// </summary>
    public class CleanRoomRoomSummary
    {
        // Out か分類済みかは型で表し、クラス名を空文字で埋めない
        // Out versus classified is told by the type; the class name is never padded with an empty string
        public CleanRoomClassStanding Class { get; }
        public double AirChangeRate { get; }
        public int Volume { get; }
        public IReadOnlyList<CleanRoomAirFilterSummary> AirFilters { get; }

        // 部屋の清浄機の台数と、そのうち止まっている（電力かフィルターが無く除去0の）台数
        // Purifiers in the room and how many of them are stopped (no power or no filter, removing nothing)
        public int AirFilterCount { get; }
        public int StoppedAirFilterCount { get; }
        public CleanRoomQueriedFrom QueriedFrom { get; }

        private CleanRoomRoomSummary(CleanRoomClassStanding standing, double airChangeRate, int volume, IReadOnlyList<CleanRoomAirFilterSummary> airFilters, int airFilterCount, int stoppedAirFilterCount, CleanRoomQueriedFrom queriedFrom)
        {
            Class = standing;
            AirChangeRate = airChangeRate;
            Volume = volume;
            AirFilters = airFilters;
            AirFilterCount = airFilterCount;
            StoppedAirFilterCount = stoppedAirFilterCount;
            QueriedFrom = queriedFrom;
        }

        public static CleanRoomRoomSummary Create(CleanRoom room, IWorldBlockDatastore world, IBlock queriedBlock, IReadOnlyList<double> requiredAirChangeRates)
        {
            // 清浄機一覧はクラス判定と同じ収集関数で集める
            // The purifier list is gathered with the same collection function as the class decision
            var entries = CleanRoomAirFilterCollector.Collect(room, world);
            var airFilters = entries.Select(entry => new CleanRoomAirFilterSummary(entry.Block.BlockPositionInfo.OriginalPos, entry.Filter.GetStatus())).ToList();

            // 換気回数はクラス判定と同じ関数で出す（体積0の部屋は0）
            // The air change rate comes from the same function as the class decision (zero for a zero-volume room)
            var airChangeRate = CleanRoomAirFilterCollector.AirChangeRate(room, entries);

            // 止まっている台は、クラス判定で除去0として数えられる台
            // A stopped unit is one the class decision counts as removing nothing
            var stoppedCount = entries.Count(entry => entry.Filter.RemovalVolumePerSecond <= 0);
            var queriedFrom = CleanRoomQueriedFrom.Of(room, entries, queriedBlock, requiredAirChangeRates);
            return new CleanRoomRoomSummary(CleanRoomClassStanding.Of(room), airChangeRate, room.Volume, airFilters, entries.Count, stoppedCount, queriedFrom);
        }
    }

    /// <summary>
    ///     部屋の照会をどのブロックから引いたか。清浄機以外・清浄機 のどちらか1つだけで、清浄機から引いたときだけ自身の状態と次のクラスまでの見通しを必ず持つ
    ///     Which block the room query came from: exactly one of non-purifier or purifier, and only the purifier kind always carries its own state and the outlook to the next class
    /// </summary>
    public abstract class CleanRoomQueriedFrom
    {
        // 種別はこのファイルの2つの型に閉じる
        // The kinds are closed to the two types in this file
        private protected CleanRoomQueriedFrom()
        {
        }

        public static CleanRoomQueriedFrom Of(CleanRoom room, IReadOnlyList<CleanRoomAirFilterEntry> entries, IBlock queriedBlock, IReadOnlyList<double> requiredAirChangeRates)
        {
            // 清浄機かどうかはここ1か所で調べる。機械の画面は部屋クラスの1行だけなので、清浄機以外からの照会は自身の状態も台数も持たない
            // The purifier check lives only here; the machine screen shows only the class line, so a non-purifier query carries neither its own state nor units
            if (!queriedBlock.TryGetComponent<ICleanRoomAirFilter>(out var queriedFilter)) return CleanRoomQueriedFromNonAirFilter.Instance;
            var nextClass = CleanRoomNextClassOutlook.Of(room, entries, queriedBlock, queriedFilter, requiredAirChangeRates);
            return new CleanRoomQueriedFromAirFilter(queriedFilter.GetStatus(), nextClass);
        }
    }

    // 清浄機以外（室内の機械）から引いた照会
    // Queried from something other than a purifier (an interior machine)
    public sealed class CleanRoomQueriedFromNonAirFilter : CleanRoomQueriedFrom
    {
        public static readonly CleanRoomQueriedFromNonAirFilter Instance = new();

        private CleanRoomQueriedFromNonAirFilter()
        {
        }
    }

    // 清浄機から引いた照会。清浄機の画面の「この清浄機」の行と「あと何台」
    // Queried from a purifier: the purifier screen's "this purifier" line and its units to the next class
    public sealed class CleanRoomQueriedFromAirFilter : CleanRoomQueriedFrom
    {
        public CleanRoomAirFilterStatus Status { get; }
        public CleanRoomNextClassOutlook NextClass { get; }

        public CleanRoomQueriedFromAirFilter(CleanRoomAirFilterStatus status, CleanRoomNextClassOutlook nextClass)
        {
            Status = status;
            NextClass = nextClass;
        }
    }
}
