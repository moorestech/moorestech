namespace Client.WebUiHost.Game.Topics.BlockDetail.CleanRoom
{
    /// <summary>
    ///     出力待ちの確定済み抽選に使った値。親の抽選の中身と同じ役割の値だけを持ち、組み立ては役割ごとの factory だけ
    ///     Values a fixed draw waiting for output used; carries only the parent draw detail's role values and is built only through per-role factories
    /// </summary>
    public class CleanRoomYieldPendingDrawDto
    {
        public readonly double? LayerFailRate;
        public readonly double? DieLossRate;
        public readonly int? QualityTier;
        public readonly double[] LevelRates;
        public readonly double? FailedRate;

        private CleanRoomYieldPendingDrawDto(double? layerFailRate, double? dieLossRate, int? qualityTier, double[] levelRates, double? failedRate)
        {
            LayerFailRate = layerFailRate;
            DieLossRate = dieLossRate;
            QualityTier = qualityTier;
            LevelRates = levelRates;
            FailedRate = failedRate;
        }

        public static CleanRoomYieldPendingDrawDto CreateLayerExposure(double layerFailRate)
        {
            return new CleanRoomYieldPendingDrawDto(layerFailRate, null, null, null, null);
        }

        public static CleanRoomYieldPendingDrawDto CreateDicing(double dieLossRate, int qualityTier, double[] levelRates, double failedRate)
        {
            return new CleanRoomYieldPendingDrawDto(null, dieLossRate, qualityTier, levelRates, failedRate);
        }
    }
}
