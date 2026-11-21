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

    // 密閉された部屋の要約。照会元（室内機械／清浄機）ごとの型で、清浄機から見た部屋は清浄機の型だけが必ず持つ（ADR 0105 §1・§5）
    // Summary of an enclosed room typed per query source; only the purifier type carries the purifier's view, always (ADR 0105 §1, §5)
    public abstract class CleanRoomSummary
    {
        public CleanRoomHeadline Headline { get; }

        private protected CleanRoomSummary(CleanRoomHeadline headline)
        {
            Headline = headline;
        }

        // 室内機械から見た部屋（部屋クラスだけ）
        // The room as an interior machine sees it (class only)
        public static CleanRoomSummary ForMachine(CleanRoomHeadline headline)
        {
            return new MachineCleanRoomSummary(headline);
        }

        public static CleanRoomSummary ForAirFilter(CleanRoomHeadline headline, CleanRoomAirFilterRoomStatus airFilter)
        {
            return new AirFilterCleanRoomSummary(headline, airFilter);
        }
    }

    public sealed class MachineCleanRoomSummary : CleanRoomSummary
    {
        internal MachineCleanRoomSummary(CleanRoomHeadline headline) : base(headline)
        {
        }
    }

    // 台数・止まっている台数・あと何台・この清浄機の状態
    // Counts, stopped count, units to the next class and this purifier's status
    public sealed class AirFilterCleanRoomSummary : CleanRoomSummary
    {
        public CleanRoomAirFilterRoomStatus AirFilter { get; }

        internal AirFilterCleanRoomSummary(CleanRoomHeadline headline, CleanRoomAirFilterRoomStatus airFilter) : base(headline)
        {
            AirFilter = airFilter;
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
