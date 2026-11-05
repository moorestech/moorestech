using System.Collections.Generic;
using System.Linq;
using Game.CleanRoom.Util;
using Game.World.Interface.DataStore;

namespace Game.CleanRoom.Info
{
    /// <summary>
    ///     部屋1つのクラス・濃度・換気回数・清浄機の状況
    ///     Class, concentration, air change rate and purifier states of one room
    /// </summary>
    public class CleanRoomRoomSummary
    {
        // Out か分類済みかは型で表し、クラス名を空文字で埋めない
        // Out versus classified is told by the type; the class name is never padded with an empty string
        public CleanRoomClassStanding Class { get; }
        public double Concentration { get; }
        public double AirChangeRate { get; }
        public int Volume { get; }
        public IReadOnlyList<CleanRoomAirFilterSummary> AirFilters { get; }

        private CleanRoomRoomSummary(CleanRoomClassStanding standing, double concentration, double airChangeRate, int volume, IReadOnlyList<CleanRoomAirFilterSummary> airFilters)
        {
            Class = standing;
            Concentration = concentration;
            AirChangeRate = airChangeRate;
            Volume = volume;
            AirFilters = airFilters;
        }

        public static CleanRoomRoomSummary Create(CleanRoom room, IWorldBlockDatastore world)
        {
            // 換気回数の分子は、純度計算と同じ収集・同じ合計関数で出す
            // The air-change numerator uses the same collection and sum function as the purity step
            var entries = CleanRoomAirFilterCollector.Collect(room, world);
            var removalVolume = CleanRoomAirFilterCollector.SumRemovalVolume(entries);
            var airFilters = entries.Select(entry => new CleanRoomAirFilterSummary(entry.Block.BlockPositionInfo.OriginalPos, entry.Filter.GetStatus())).ToList();

            // 濃度・換気回数はクラス判定と同じ関数で出す（体積0の部屋は0）
            // Concentration and air change rate come from the same functions as the class decision (zero for a zero-volume room)
            var concentration = CleanRoomPurityLogic.Concentration(room.ImpurityCount, room.Volume);
            var airChangeRate = CleanRoomPurityLogic.AirChangeRate(removalVolume, room.Volume);
            return new CleanRoomRoomSummary(CleanRoomClassStanding.Of(room), concentration, airChangeRate, room.Volume, airFilters);
        }
    }
}
