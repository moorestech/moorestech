using Core.Master;

namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     部屋クラスの歩留まり値と閾値行の添字
    ///     Per-class yield values and their threshold row index
    /// </summary>
    public readonly struct CleanRoomClassYield
    {
        // 閾値行の添字。0 がいちばん良いクラス
        // Threshold row index; 0 is the best class
        public readonly int ThresholdIndex;
        public readonly float QualityMultiplier;
        public readonly float LayerFailRate;
        public readonly float DieLossRate;

        public CleanRoomClassYield(int thresholdIndex, float qualityMultiplier, float layerFailRate, float dieLossRate)
        {
            ThresholdIndex = thresholdIndex;
            QualityMultiplier = qualityMultiplier;
            LayerFailRate = layerFailRate;
            DieLossRate = dieLossRate;
        }

        // 閾値行から歩留まり値を作る
        // Creates yield values from a threshold row
        public static CleanRoomClassYield FromThresholdIndex(int thresholdIndex)
        {
            var row = MasterHolder.CleanRoomMaster.Thresholds[thresholdIndex];
            return new CleanRoomClassYield(thresholdIndex, row.QualityMultiplier, row.LayerFailRate, row.DieLossRate);
        }

        // 今より1つ上の行を返す。最良行には昇格先が無い
        // Returns the row one level above; the best row has no promotion target
        public static bool TryGetPromotionTargetIndex(int currentIndex, out int targetIndex)
        {
            targetIndex = currentIndex - 1;
            return 0 < currentIndex;
        }
    }
}
