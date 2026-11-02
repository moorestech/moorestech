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

        // fetched のときだけ: layerExposure / dicing
        // Only when fetched: layerExposure / dicing
        public readonly string Role;

        // layerExposure のときだけ
        // Only for layerExposure
        public readonly double? LayerFailRate;

        // dicing のときだけ。レベル別割合と失敗ダイ割合は損失を除いた良品に対する割合
        // Only for dicing; the per-level and failed-die rates are over survivors (losses excluded)
        public readonly double? DieLossRate;
        public readonly int? QualityTier;
        public readonly double[] LevelRates;
        public readonly double? FailedRate;

        // 出力の空きを待っている確定済み抽選に使った値（同じ役割）。待っていなければ null でキーごと省く
        // Values a fixed draw waiting for output space used (same role); null and omitted with its key when nothing waits
        public readonly CleanRoomYieldPendingDrawDto PendingDraw;

        private CleanRoomYieldDetailDto(string fetchState, string role, double? layerFailRate, double? dieLossRate, int? qualityTier, double[] levelRates, double? failedRate, CleanRoomYieldPendingDrawDto pendingDraw)
        {
            FetchState = fetchState;
            Role = role;
            LayerFailRate = layerFailRate;
            DieLossRate = dieLossRate;
            QualityTier = qualityTier;
            LevelRates = levelRates;
            FailedRate = failedRate;
            PendingDraw = pendingDraw;
        }

        public static CleanRoomYieldDetailDto CreateLayerExposure(double layerFailRate, CleanRoomYieldPendingDrawDto pendingDraw)
        {
            return new CleanRoomYieldDetailDto(BlockInfoFetchStateWireName.Fetched, "layerExposure", layerFailRate, null, null, null, null, pendingDraw);
        }

        public static CleanRoomYieldDetailDto CreateDicing(double dieLossRate, int qualityTier, double[] levelRates, double failedRate, CleanRoomYieldPendingDrawDto pendingDraw)
        {
            return new CleanRoomYieldDetailDto(BlockInfoFetchStateWireName.Fetched, "dicing", null, dieLossRate, qualityTier, levelRates, failedRate, pendingDraw);
        }

        // 取得できていない間は取得状態だけを載せ、率を作らない
        // While not fetched, carry only the fetch state and make up no rates
        public static CleanRoomYieldDetailDto CreateUnavailable(BlockInfoFetchState fetchState)
        {
            return new CleanRoomYieldDetailDto(BlockInfoFetchStateWireName.ToUnavailableWireName(fetchState), null, null, null, null, null, null, null);
        }
    }
}
