using System.Collections.Generic;
using System.Linq;
using Game.CleanRoom.Util;
using Game.World.Interface.DataStore;

namespace Game.CleanRoom.Info
{
    /// <summary>
    ///     部屋1つのクラス・換気回数・清浄機の状況
    ///     Class, air change rate and purifier states of one room
    /// </summary>
    public class CleanRoomRoomSummary
    {
        // Out か分類済みかは型で表し、クラス名を空文字で埋めない
        // Out versus classified is told by the type; the class name is never padded with an empty string
        public CleanRoomClassStanding Class { get; }
        public double AirChangeRate { get; }
        public int Volume { get; }
        public IReadOnlyList<CleanRoomAirFilterSummary> AirFilters { get; }

        private CleanRoomRoomSummary(CleanRoomClassStanding standing, double airChangeRate, int volume, IReadOnlyList<CleanRoomAirFilterSummary> airFilters)
        {
            Class = standing;
            AirChangeRate = airChangeRate;
            Volume = volume;
            AirFilters = airFilters;
        }

        public static CleanRoomRoomSummary Create(CleanRoom room, IWorldBlockDatastore world)
        {
            // 清浄機一覧はクラス判定と同じ収集関数で集める
            // The purifier list is gathered with the same collection function as the class decision
            var entries = CleanRoomAirFilterCollector.Collect(room, world);
            var airFilters = entries.Select(entry => new CleanRoomAirFilterSummary(entry.Block.BlockPositionInfo.OriginalPos, entry.Filter.GetStatus())).ToList();

            // 換気回数はクラス判定と同じ関数で出す（体積0の部屋は0）
            // The air change rate comes from the same function as the class decision (zero for a zero-volume room)
            var airChangeRate = CleanRoomAirFilterCollector.AirChangeRate(room, entries);
            return new CleanRoomRoomSummary(CleanRoomClassStanding.Of(room), airChangeRate, room.Volume, airFilters);
        }
    }
}
