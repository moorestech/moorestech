using Client.Game.InGame.BlockInfoPolling;
using Client.WebUiHost.Game.Topics.BlockDetail.FetchState;

namespace Client.WebUiHost.Game.Topics.BlockDetail.CleanRoom
{
    /// <summary>
    ///     室内機械の選択工程の抽選の中身。組み立ては役割ごとの factory だけで、null のフィールドはキーごと省かれる
    ///     Draw details of an interior machine's selected process; built only through per-role factories, and null fields are omitted with their keys
    /// </summary>
    public class CleanRoomYieldDetailDto
    {
        // fetched / notFetched / transportFailed / unreadable / unsupportedTarget / vanished
        public readonly string FetchState;

        // fetched のときだけ: waferGrade / layerExposure / dicing
        // Only when fetched: waferGrade / layerExposure / dicing
        public readonly string Role;

        // waferGrade のときだけ
        // Only for waferGrade
        public readonly int? MaxGrade;
        public readonly double? PromotionChance;

        // layerExposure のときだけ
        // Only for layerExposure
        public readonly double? LayerFailRate;

        // dicing のときだけ
        // Only for dicing
        public readonly double? DieLossRate;

        private CleanRoomYieldDetailDto(string fetchState, string role, int? maxGrade, double? promotionChance, double? layerFailRate, double? dieLossRate)
        {
            FetchState = fetchState;
            Role = role;
            MaxGrade = maxGrade;
            PromotionChance = promotionChance;
            LayerFailRate = layerFailRate;
            DieLossRate = dieLossRate;
        }

        public static CleanRoomYieldDetailDto CreateWaferGrade(int maxGrade, double promotionChance)
        {
            return new CleanRoomYieldDetailDto(BlockInfoFetchStateWireName.Fetched, "waferGrade", maxGrade, promotionChance, null, null);
        }

        public static CleanRoomYieldDetailDto CreateLayerExposure(double layerFailRate)
        {
            return new CleanRoomYieldDetailDto(BlockInfoFetchStateWireName.Fetched, "layerExposure", null, null, layerFailRate, null);
        }

        public static CleanRoomYieldDetailDto CreateDicing(double dieLossRate)
        {
            return new CleanRoomYieldDetailDto(BlockInfoFetchStateWireName.Fetched, "dicing", null, null, null, dieLossRate);
        }

        // 取得できていない間は取得状態だけを載せ、率を作らない
        // While not fetched, carry only the fetch state and make up no rates
        public static CleanRoomYieldDetailDto CreateUnavailable(BlockInfoFetchState fetchState)
        {
            return new CleanRoomYieldDetailDto(BlockInfoFetchStateWireName.ToUnavailableWireName(fetchState), null, null, null, null, null);
        }
    }
}
