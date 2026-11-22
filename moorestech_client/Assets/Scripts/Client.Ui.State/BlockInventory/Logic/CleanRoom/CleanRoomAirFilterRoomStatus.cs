namespace Client.Ui.State.BlockInventory.Logic.CleanRoom
{
    /// <summary>
    /// 清浄機から見た部屋: 台数・止まっている台数・次のクラスまでの台数・照会した清浄機の電力と残量（値はサーバーが数えたもの。ADR 0105 §5）
    /// The room as a purifier sees it: counts, stopped count, units to the next class and the queried purifier's power and life (server-counted; ADR 0105 §5)
    /// </summary>
    public sealed class CleanRoomAirFilterRoomStatus
    {
        public int AirFilterCount { get; }
        public int StoppedAirFilterCount { get; }
        public double PowerRatio { get; }
        public double FilterRemainingRatio { get; }

        // 次のクラスまでの台数。台数で届くときだけ台数を持つ型（最上位・届かない・数えていないは持たない型）
        // Units to the next class; only the reachable type carries numbers (not at best, unreachable or not counted)
        private readonly CleanRoomUnitsToNextClass _unitsToNextClass;

        private CleanRoomAirFilterRoomStatus(int airFilterCount, int stoppedAirFilterCount, double powerRatio, double filterRemainingRatio, CleanRoomUnitsToNextClass unitsToNextClass)
        {
            AirFilterCount = airFilterCount;
            StoppedAirFilterCount = stoppedAirFilterCount;
            PowerRatio = powerRatio;
            FilterRemainingRatio = filterRemainingRatio;
            _unitsToNextClass = unitsToNextClass;
        }

        public static CleanRoomAirFilterRoomStatus WithUnitsToNextClass(int airFilterCount, int stoppedAirFilterCount, double powerRatio, double filterRemainingRatio, string nextClassName, int units)
        {
            return new CleanRoomAirFilterRoomStatus(airFilterCount, stoppedAirFilterCount, powerRatio, filterRemainingRatio, new ReachableUnitsToNextClass(nextClassName, units));
        }

        public static CleanRoomAirFilterRoomStatus WithoutUnitsToNextClass(int airFilterCount, int stoppedAirFilterCount, double powerRatio, double filterRemainingRatio)
        {
            return new CleanRoomAirFilterRoomStatus(airFilterCount, stoppedAirFilterCount, powerRatio, filterRemainingRatio, NoUnitsToNextClass.Instance);
        }

        public bool TryGetUnitsToNextClass(out string nextClassName, out int units)
        {
            if (_unitsToNextClass is ReachableUnitsToNextClass reachable)
            {
                nextClassName = reachable.NextClassName;
                units = reachable.Units;
                return true;
            }
            nextClassName = default;
            units = default;
            return false;
        }
    }

    // 次のクラスまでの台数の種別。台数と次のクラス名は届く型だけが持つ
    // Kinds of units-to-next-class; only the reachable kind holds the count and next class name
    internal abstract class CleanRoomUnitsToNextClass
    {
        private protected CleanRoomUnitsToNextClass()
        {
        }
    }

    internal sealed class ReachableUnitsToNextClass : CleanRoomUnitsToNextClass
    {
        public string NextClassName { get; }
        public int Units { get; }

        internal ReachableUnitsToNextClass(string nextClassName, int units)
        {
            NextClassName = nextClassName;
            Units = units;
        }
    }

    internal sealed class NoUnitsToNextClass : CleanRoomUnitsToNextClass
    {
        public static readonly NoUnitsToNextClass Instance = new();

        private NoUnitsToNextClass()
        {
        }
    }
}
