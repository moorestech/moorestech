using System;
using Core.Master;
using Mooresmaster.Model.TrainModule;

namespace Game.Train.Unit.Motion
{
    /// <summary>
    /// 編成の車両を積み上げ、重量の影響度を反映した実効重量を求める
    /// Accumulates a train's cars and computes the effective weight with the weight influence exponent applied
    /// </summary>
    public struct TrainEffectiveWeightCalculator
    {
        private int _totalWeight;
        private double _totalTraction;
        private double _tractionWeightedExponentSum;

        public void AddCar(int carWeight, TrainCarMasterElement carMaster)
        {
            // 影響度を牽引力で重み付け（貨車は効かない）
            // Weight the exponent by traction so zero-traction cars (wagons) have no effect
            _totalWeight += carWeight;
            _totalTraction += carMaster.TractionForce;
            _tractionWeightedExponentSum += carMaster.TractionForce * (double)carMaster.WeightInfluenceExponent;
        }

        public double CalculateEffectiveWeight()
        {
            // 牽引力0の編成は実重量（影響度1）
            // A zero-traction train uses its real weight (exponent 1)
            var exponent = _totalTraction > 0 ? _tractionWeightedExponentSum / _totalTraction : 1d;
            return Calculate(_totalWeight, exponent, MasterHolder.TrainUnitMaster.ReferenceWeight);
        }

        // 基準重量×(重量/基準重量)^影響度 を、影響度1で実重量と厳密一致する形に変形して計算
        // referenceWeight*(weight/referenceWeight)^exponent, rearranged so exponent 1 returns the real weight exactly
        internal static double Calculate(int totalWeight, double weightInfluenceExponent, int referenceWeight)
        {
            return totalWeight * Math.Pow(totalWeight / (double)referenceWeight, weightInfluenceExponent - 1);
        }
    }
}
