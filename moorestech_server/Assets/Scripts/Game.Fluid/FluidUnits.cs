using System;

namespace Game.Fluid
{
    // パイプ網の流体量の単位。1 単位 = 流体量 1/1000。網の量・容量・速度・上限はすべてこの単位の long で持ち、機械側の double とはここで変換する(ADR 0090)
    // The unit of fluid amounts in the pipe network: one unit is 1/1000 of an amount. Network amounts, capacities, velocities and caps are longs in this unit; conversion to the machine side's double happens here (ADR 0090)
    public static class FluidUnits
    {
        public const long UnitsPerAmount = 1000;

        // 機械→パイプの受け入れは切り捨て(端数は機械に残る)。増える側には丸めない
        // Machine-to-pipe acceptance rounds down (the fraction stays in the machine); never rounds toward creating fluid
        public static long ToUnitsFloor(double amount)
        {
            return (long)Math.Floor(amount * UnitsPerAmount);
        }

        // パイプ→機械でノードから引く量は切り上げ(ノードが引く量 ≥ 機械が得た量)
        // The amount a node loses on delivery rounds up (the node loses at least what the machine gained)
        public static long ToUnitsCeil(double amount)
        {
            return (long)Math.Ceiling(amount * UnitsPerAmount);
        }

        // マスタの容量・上限やセーブの移行など、増減の向きが無い値は最近傍
        // Values with no creation/destruction direction (master capacities, caps, save migration) round to nearest
        public static long ToUnitsNearest(double amount)
        {
            return (long)Math.Round(amount * UnitsPerAmount, MidpointRounding.AwayFromZero);
        }

        public static double ToAmount(long units)
        {
            return units / (double)UnitsPerAmount;
        }
    }
}
