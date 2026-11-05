using System;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route
{
    /// <summary>
    ///     一歩の種類。水平X・水平Z・坂（Y変化）の3種
    ///     The kind of one step: horizontal X, horizontal Z, or a slope (Y change)
    /// </summary>
    public enum BeltRouteStepAxis
    {
        X = 0,
        Y = 1,
        Z = 2,
    }

    /// <summary>
    ///     同コストの候補をどの軸の一歩から試すかの順。X/Y/Zの並べ替え6通りをCキーで送る
    ///     The order in which equal-cost candidates are tried by step axis; the six permutations of X/Y/Z cycle with the C key
    /// </summary>
    public readonly struct BeltRouteStepPriority : IEquatable<BeltRouteStepPriority>
    {
        public const int PermutationCount = 6;

        private static readonly BeltRouteStepAxis[][] Permutations =
        {
            new[] { BeltRouteStepAxis.X, BeltRouteStepAxis.Y, BeltRouteStepAxis.Z },
            new[] { BeltRouteStepAxis.X, BeltRouteStepAxis.Z, BeltRouteStepAxis.Y },
            new[] { BeltRouteStepAxis.Y, BeltRouteStepAxis.X, BeltRouteStepAxis.Z },
            new[] { BeltRouteStepAxis.Y, BeltRouteStepAxis.Z, BeltRouteStepAxis.X },
            new[] { BeltRouteStepAxis.Z, BeltRouteStepAxis.X, BeltRouteStepAxis.Y },
            new[] { BeltRouteStepAxis.Z, BeltRouteStepAxis.Y, BeltRouteStepAxis.X },
        };

        public readonly int Index;

        public BeltRouteStepPriority(int index)
        {
            Index = ((index % PermutationCount) + PermutationCount) % PermutationCount;
        }

        public static BeltRouteStepPriority Default => new(0);

        public BeltRouteStepPriority Next() => new(Index + 1);

        // 小さいほど先に試す
        // Smaller ranks are tried first
        public int Rank(BeltRouteStepAxis axis)
        {
            var order = Permutations[Index];
            return Array.IndexOf(order, axis);
        }

        public bool Equals(BeltRouteStepPriority other) => Index == other.Index;
        public override bool Equals(object obj) => obj is BeltRouteStepPriority other && Equals(other);
        public override int GetHashCode() => Index;
    }
}
