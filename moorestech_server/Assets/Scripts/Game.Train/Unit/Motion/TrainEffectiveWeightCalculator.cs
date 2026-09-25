using System;
using Core.Master;

namespace Game.Train.Unit.Motion
{
    /// <summary>
    /// 重量の影響度を反映した、加速・空気抵抗に使う実効重量の計算
    /// Effective weight used for acceleration and air resistance, applying the weight influence exponent
    /// </summary>
    internal static class TrainEffectiveWeightCalculator
    {
        internal static double CalculateFromMaster(int totalWeight)
        {
            var master = MasterHolder.TrainUnitMaster;
            return Calculate(totalWeight, master.WeightInfluenceExponent, master.ReferenceWeight);
        }

        // 基準重量×(重量/基準重量)^影響度 を、影響度1で実重量と厳密一致する形に変形して計算
        // referenceWeight*(weight/referenceWeight)^exponent, rearranged so exponent 1 returns the real weight exactly
        internal static double Calculate(int totalWeight, double weightInfluenceExponent, int referenceWeight)
        {
            return totalWeight * Math.Pow(totalWeight / (double)referenceWeight, weightInfluenceExponent - 1);
        }
    }
}
