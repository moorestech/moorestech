using System.Collections.Generic;

namespace Client.Ui.State.BlockInventory.Logic.CleanRoom
{
    // 見出し。Out の判定はサーバーが行い、Out はクラス名を持たない（判断記録 D-5）
    // The headline; the server decides Out, and Out carries no class name (D-5)
    public abstract class CleanRoomHeadline
    {
        public static readonly CleanRoomHeadline ClassOut = new ClassOutCleanRoomHeadline();

        private protected CleanRoomHeadline()
        {
        }

        public static CleanRoomHeadline Class(string className)
        {
            return new ClassCleanRoomHeadline(className);
        }
    }

    public sealed class ClassCleanRoomHeadline : CleanRoomHeadline
    {
        public string ClassName { get; }

        internal ClassCleanRoomHeadline(string className)
        {
            ClassName = className;
        }
    }

    public sealed class ClassOutCleanRoomHeadline : CleanRoomHeadline
    {
        internal ClassOutCleanRoomHeadline()
        {
        }
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

        private protected CleanRoomFetchedDetail(CleanRoomHaltReason haltReason)
        {
            HaltReason = haltReason;
        }

        public static CleanRoomFetchedDetail Enclosed(CleanRoomSummary room, CleanRoomHaltReason haltReason)
        {
            return new EnclosedCleanRoomDetail(room, haltReason);
        }

        public static CleanRoomFetchedDetail Openings(int openingCount, CleanRoomHaltReason haltReason)
        {
            return new OpeningsCleanRoomDetail(openingCount, haltReason);
        }

        public static CleanRoomFetchedDetail NoEnclosure(CleanRoomHaltReason haltReason)
        {
            return new NoEnclosureCleanRoomDetail(haltReason);
        }

        public static CleanRoomFetchedDetail TooLarge(int maxRoomVolume, CleanRoomHaltReason haltReason)
        {
            return new TooLargeCleanRoomDetail(maxRoomVolume, haltReason);
        }

        public static CleanRoomFetchedDetail Detecting(CleanRoomHaltReason haltReason)
        {
            return new DetectingCleanRoomDetail(haltReason);
        }
    }

    public sealed class EnclosedCleanRoomDetail : CleanRoomFetchedDetail
    {
        public CleanRoomSummary Room { get; }

        internal EnclosedCleanRoomDetail(CleanRoomSummary room, CleanRoomHaltReason haltReason) : base(haltReason)
        {
            Room = room;
        }
    }

    public sealed class OpeningsCleanRoomDetail : CleanRoomFetchedDetail
    {
        public int OpeningCount { get; }

        internal OpeningsCleanRoomDetail(int openingCount, CleanRoomHaltReason haltReason) : base(haltReason)
        {
            OpeningCount = openingCount;
        }
    }

    public sealed class NoEnclosureCleanRoomDetail : CleanRoomFetchedDetail
    {
        internal NoEnclosureCleanRoomDetail(CleanRoomHaltReason haltReason) : base(haltReason)
        {
        }
    }

    public sealed class TooLargeCleanRoomDetail : CleanRoomFetchedDetail
    {
        public int MaxRoomVolume { get; }

        internal TooLargeCleanRoomDetail(int maxRoomVolume, CleanRoomHaltReason haltReason) : base(haltReason)
        {
            MaxRoomVolume = maxRoomVolume;
        }
    }

    public sealed class DetectingCleanRoomDetail : CleanRoomFetchedDetail
    {
        internal DetectingCleanRoomDetail(CleanRoomHaltReason haltReason) : base(haltReason)
        {
        }
    }
}
