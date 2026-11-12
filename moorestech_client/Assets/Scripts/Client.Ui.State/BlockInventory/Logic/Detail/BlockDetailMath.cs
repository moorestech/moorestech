using System;

namespace Client.Ui.State.BlockInventory.Logic.Detail
{
    /// <summary>
    /// ブロック詳細の数値計算。Web の detailLogic.ts の計算部分の写し
    /// Numeric helpers for block details; a port of the arithmetic in Web detailLogic.ts
    /// </summary>
    public static class BlockDetailMath
    {
        // 毎秒→毎分の換算の 1 か所（採掘・ポンプ・レシピ・ベルトの毎分がこれを使う）
        // The single seconds-to-minutes factor (miner, pump, recipe and belt per-minute figures use it)
        public const double SecondsPerMinute = 60;

        // uGUI CommonMachineBlockStateDetail.PowerRate と同式。要求 0 は充足 1 とみなす
        // Same formula as uGUI CommonMachineBlockStateDetail.PowerRate; a zero request counts as fully met
        public static double ComputePowerRate(double currentPower, double requestPower)
        {
            return requestPower == 0 ? 1 : currentPower / requestPower;
        }

        // レシピ秒が 0 以下なら毎分個数は出さない（Web は null）
        // No per-minute figure when the recipe time is zero or less (null on the Web)
        public static bool TryGetItemsPerMinute(int outputCount, double recipeTimeSeconds, out double itemsPerMinute)
        {
            itemsPerMinute = recipeTimeSeconds <= 0 ? 0 : outputCount * SecondsPerMinute / recipeTimeSeconds;
            return 0 < recipeTimeSeconds;
        }

        // 0..total-1 の全スロット添字。全スロットを並べるビューはこの定義を使う
        // All slot indices 0..total-1; views listing every slot use this single definition
        public static int[] AllSlotIndices(int total)
        {
            var indices = new int[Math.Max(0, total)];
            for (var i = 0; i < indices.Length; i++) indices[i] = i;
            return indices;
        }

        // 残燃料/満燃料を 0..1 に収める。分母 0 は 0（uGUI 発電機の燃料バー相当）
        // Clamp remaining/full fuel to 0..1; a zero denominator gives 0 (mirrors the uGUI generator fuel bar)
        public static double FuelRatio(double remainingFuelTime, double currentFuelTime)
        {
            if (currentFuelTime <= 0) return 0;
            return Math.Min(1, Math.Max(0, remainingFuelTime / currentFuelTime));
        }
    }
}
