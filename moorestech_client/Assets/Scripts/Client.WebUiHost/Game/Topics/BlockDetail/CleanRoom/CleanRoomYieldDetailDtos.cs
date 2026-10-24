namespace Client.WebUiHost.Game.Topics.BlockDetail.CleanRoom
{
    /// <summary>
    ///     室内機械の選択工程の抽選の中身。null のフィールドはキーごと省かれる（役割ごとの形は TS の zod が閉じる）
    ///     Draw details of an interior machine's selected process; null fields are omitted with their keys (the TS zod schema closes the per-role shapes)
    /// </summary>
    public class CleanRoomYieldDetailDto
    {
        // waferGrade / layerExposure / dicing
        public string Role;

        // waferGrade のときだけ
        // Only for waferGrade
        public int? MaxGrade;
        public double? PromotionChance;

        // layerExposure のときだけ
        // Only for layerExposure
        public double? LayerFailRate;

        // dicing のときだけ
        // Only for dicing
        public double? DieLossRate;
    }
}
