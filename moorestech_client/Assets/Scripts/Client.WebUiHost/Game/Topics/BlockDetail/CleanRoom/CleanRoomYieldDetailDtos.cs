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

        // 出力の空きを待っている確定済み抽選に使った値（同じ役割）。待っていなければ null でキーごと省く
        // Values a fixed draw waiting for output space used (same role); null and omitted with its key when nothing waits
        public readonly CleanRoomYieldPendingDrawDto PendingDraw;

        private CleanRoomYieldDetailDto(string fetchState, string role, int? maxGrade, double? promotionChance, double? layerFailRate, double? dieLossRate, CleanRoomYieldPendingDrawDto pendingDraw)
        {
            FetchState = fetchState;
            Role = role;
            MaxGrade = maxGrade;
            PromotionChance = promotionChance;
            LayerFailRate = layerFailRate;
            DieLossRate = dieLossRate;
            PendingDraw = pendingDraw;
        }

        public static CleanRoomYieldDetailDto CreateWaferGrade(int maxGrade, double promotionChance, CleanRoomYieldPendingDrawDto pendingDraw)
        {
            return new CleanRoomYieldDetailDto(BlockInfoFetchStateWireName.Fetched, "waferGrade", maxGrade, promotionChance, null, null, pendingDraw);
        }

        public static CleanRoomYieldDetailDto CreateLayerExposure(double layerFailRate, CleanRoomYieldPendingDrawDto pendingDraw)
        {
            return new CleanRoomYieldDetailDto(BlockInfoFetchStateWireName.Fetched, "layerExposure", null, null, layerFailRate, null, pendingDraw);
        }

        public static CleanRoomYieldDetailDto CreateDicing(double dieLossRate, CleanRoomYieldPendingDrawDto pendingDraw)
        {
            return new CleanRoomYieldDetailDto(BlockInfoFetchStateWireName.Fetched, "dicing", null, null, null, dieLossRate, pendingDraw);
        }

        // 取得できていない間は取得状態だけを載せ、率を作らない
        // While not fetched, carry only the fetch state and make up no rates
        public static CleanRoomYieldDetailDto CreateUnavailable(BlockInfoFetchState fetchState)
        {
            return new CleanRoomYieldDetailDto(BlockInfoFetchStateWireName.ToUnavailableWireName(fetchState), null, null, null, null, null, null);
        }
    }
}
