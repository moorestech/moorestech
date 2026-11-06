using System.Collections.Generic;

namespace Client.Ui.State.BlockInventory.Logic.CleanRoom
{
    // 見出し。Out の判定はサーバーが行い、Out はクラス名を持たない（判断記録 D-5）
    // The headline; the server decides Out, and Out carries no class name (D-5)
    public abstract class CleanRoomHeadline
    {
        public static readonly CleanRoomHeadline ClassOut = new ClassOutCleanRoomHeadline();

        public static CleanRoomHeadline Class(string className)
        {
            return new ClassCleanRoomHeadline(className);
        }
    }

    public sealed class ClassCleanRoomHeadline : CleanRoomHeadline
    {
        public string ClassName { get; }

        public ClassCleanRoomHeadline(string className)
        {
            ClassName = className;
        }
    }

    public sealed class ClassOutCleanRoomHeadline : CleanRoomHeadline
    {
    }

    public enum CleanRoomHaltReason
    {
        None,
        NotEnclosed,
        ClassOut,
    }

    public readonly struct CleanRoomAirFilter
    {
        public double PowerRatio { get; }
        public int FilterItemCount { get; }
        public double FilterRemainingRatio { get; }

        public CleanRoomAirFilter(double powerRatio, int filterItemCount, double filterRemainingRatio)
        {
            PowerRatio = powerRatio;
            FilterItemCount = filterItemCount;
            FilterRemainingRatio = filterRemainingRatio;
        }
    }

    // 密閉された部屋の要約
    // Summary of an enclosed room
    public sealed class CleanRoomSummary
    {
        public CleanRoomHeadline Headline { get; }
        public double Concentration { get; }
        public double AirChangeRate { get; }
        public IReadOnlyList<CleanRoomAirFilter> AirFilters { get; }

        public CleanRoomSummary(CleanRoomHeadline headline, double concentration, double airChangeRate, IReadOnlyList<CleanRoomAirFilter> airFilters)
        {
            Headline = headline;
            Concentration = concentration;
            AirChangeRate = airChangeRate;
            AirFilters = airFilters;
        }
    }

    /// <summary>
    /// 取得済みの部屋の状態（Web の CleanRoomFetchedDetailData の kind ごとの判別 union）
    /// A fetched room state (the per-kind discriminated union of Web CleanRoomFetchedDetailData)
    /// </summary>
    public abstract class CleanRoomFetchedDetail
    {
        public CleanRoomHaltReason HaltReason { get; }

        protected CleanRoomFetchedDetail(CleanRoomHaltReason haltReason)
        {
            HaltReason = haltReason;
        }
    }

    public sealed class EnclosedCleanRoomDetail : CleanRoomFetchedDetail
    {
        public CleanRoomSummary Room { get; }

        public EnclosedCleanRoomDetail(CleanRoomSummary room, CleanRoomHaltReason haltReason) : base(haltReason)
        {
            Room = room;
        }
    }

    public sealed class OpeningsCleanRoomDetail : CleanRoomFetchedDetail
    {
        public int OpeningCount { get; }

        public OpeningsCleanRoomDetail(int openingCount, CleanRoomHaltReason haltReason) : base(haltReason)
        {
            OpeningCount = openingCount;
        }
    }

    public sealed class NoEnclosureCleanRoomDetail : CleanRoomFetchedDetail
    {
        public NoEnclosureCleanRoomDetail(CleanRoomHaltReason haltReason) : base(haltReason)
        {
        }
    }

    public sealed class TooLargeCleanRoomDetail : CleanRoomFetchedDetail
    {
        public int MaxRoomVolume { get; }

        public TooLargeCleanRoomDetail(int maxRoomVolume, CleanRoomHaltReason haltReason) : base(haltReason)
        {
            MaxRoomVolume = maxRoomVolume;
        }
    }

    public sealed class DetectingCleanRoomDetail : CleanRoomFetchedDetail
    {
        public DetectingCleanRoomDetail(CleanRoomHaltReason haltReason) : base(haltReason)
        {
        }
    }
}
