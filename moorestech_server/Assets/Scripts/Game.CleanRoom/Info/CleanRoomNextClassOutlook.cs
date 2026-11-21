using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom.Util;

namespace Game.CleanRoom.Info
{
    /// <summary>
    ///     清浄機の画面に出す次のクラスまでの見通し。いちばん良いクラス／あと N 台／空きを埋めても届かない のどれか1つ
    ///     Outlook to the next class for the purifier screen: exactly one of best class, N more units, or unreachable even when filled
    /// </summary>
    public abstract class CleanRoomNextClassOutlook
    {
        // 種別はこのファイルの型に閉じる
        // The kinds are closed to the types in this file
        private protected CleanRoomNextClassOutlook()
        {
        }

        // 今の稼働状態のまま（止まっている台は除去0）、照会した清浄機と同じ種類を電力100%・フィルターありで1台ずつ足して数える
        // Count from the current operating state (stopped units remove nothing), adding units of the queried purifier's kind at full power with a filter
        public static CleanRoomNextClassOutlook Of(CleanRoom room, IReadOnlyList<CleanRoomAirFilterEntry> entries, IBlock queriedBlock, ICleanRoomAirFilter queriedFilter, IReadOnlyList<double> requiredAirChangeRates)
        {
            // いちばん良いクラスかと次のクラスの行は、台数の数え上げと同じ口で決める（Out〈行数〉のときはいちばん下の行）
            // The best-class check and the next class row come from the same entry point as the unit count (the lowest row from Out, the row count)
            if (!CleanRoomClassYield.TryGetPromotionTargetIndex(room.ThresholdIndex, out var nextClassIndex)) return CleanRoomAtBestClassOutlook.Instance;
            var nextClassName = MasterHolder.CleanRoomMaster.Thresholds[nextClassIndex].ClassName;
            var removal = CleanRoomAirFilterCollector.SumRemovalVolume(entries);
            var unitCellCount = queriedBlock.BlockPositionInfo.EnumeratePositions().Count();
            if (!CleanRoomPurityLogic.TryCountUnitsToPromote(room.ThresholdIndex, removal, room.Volume, queriedFilter.RatedRemovalVolumePerSecond, unitCellCount, requiredAirChangeRates, out var units))
            {
                return new CleanRoomNextClassUnreachableOutlook(nextClassName);
            }
            return new CleanRoomUnitsToNextClassOutlook(nextClassName, units);
        }
    }

    // いちばん良いクラス。次のクラスは無い
    // The best class; there is no next class
    public sealed class CleanRoomAtBestClassOutlook : CleanRoomNextClassOutlook
    {
        public static readonly CleanRoomAtBestClassOutlook Instance = new();

        private CleanRoomAtBestClassOutlook()
        {
        }
    }

    public sealed class CleanRoomUnitsToNextClassOutlook : CleanRoomNextClassOutlook
    {
        public string NextClassName { get; }
        public int Units { get; }

        public CleanRoomUnitsToNextClassOutlook(string nextClassName, int units)
        {
            NextClassName = nextClassName;
            Units = units;
        }
    }

    // 空きセルを全部清浄機で埋めても次のクラスに届かない
    // Even filling every free cell with purifiers does not reach the next class
    public sealed class CleanRoomNextClassUnreachableOutlook : CleanRoomNextClassOutlook
    {
        public string NextClassName { get; }

        public CleanRoomNextClassUnreachableOutlook(string nextClassName)
        {
            NextClassName = nextClassName;
        }
    }
}
