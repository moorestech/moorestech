using System;
using Core.Master;

namespace Game.Train.Unit.Motion
{
    /// <summary>
    /// 重量の影響度を反映した、加速・空気抵抗に使う実効重量の計算
    /// Effective weight used for acceleration and air resistance, applying the weight influence exponent
    /// </summary>
    public static class TrainEffectiveWeightCalculator
    {
        public static double CalculateFromMaster(int totalWeight)
        {
            var master = MasterHolder.TrainUnitMaster;
            return Calculate(totalWeight, master.WeightInfluenceExponent, master.ReferenceWeight);
        }

        // 基準重量で正規化してから指数をかけ、基準重量ちょうどの編成は影響度に依らず同じ重さにする
        // Normalize by the reference weight before the exponent so a reference-weight train is unaffected by it
        public static double Calculate(int totalWeight, double weightInfluenceExponent, int referenceWeight)
        {
            return referenceWeight * Math.Pow(totalWeight / (double)referenceWeight, weightInfluenceExponent);
        }
    }
}
