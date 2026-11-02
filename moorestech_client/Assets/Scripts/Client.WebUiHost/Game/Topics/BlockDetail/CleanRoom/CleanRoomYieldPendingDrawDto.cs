namespace Client.WebUiHost.Game.Topics.BlockDetail.CleanRoom
{
    /// <summary>
    ///     出力待ちの確定済み抽選に使った値。親の抽選の中身と同じ役割の値だけを持ち、組み立ては役割ごとの factory だけ
    ///     Values a fixed draw waiting for output used; carries only the parent draw detail's role values and is built only through per-role factories
    /// </summary>
    public class CleanRoomYieldPendingDrawDto
    {
        public readonly int? MaxGrade;
        public readonly double? PromotionChance;
        public readonly double? LayerFailRate;
        public readonly double? DieLossRate;

        private CleanRoomYieldPendingDrawDto(int? maxGrade, double? promotionChance, double? layerFailRate, double? dieLossRate)
        {
            MaxGrade = maxGrade;
            PromotionChance = promotionChance;
            LayerFailRate = layerFailRate;
            DieLossRate = dieLossRate;
        }

        public static CleanRoomYieldPendingDrawDto CreateWaferGrade(int maxGrade, double promotionChance)
        {
            return new CleanRoomYieldPendingDrawDto(maxGrade, promotionChance, null, null);
        }

        public static CleanRoomYieldPendingDrawDto CreateLayerExposure(double layerFailRate)
        {
            return new CleanRoomYieldPendingDrawDto(null, null, layerFailRate, null);
        }

        public static CleanRoomYieldPendingDrawDto CreateDicing(double dieLossRate)
        {
            return new CleanRoomYieldPendingDrawDto(null, null, null, dieLossRate);
        }
    }
}
