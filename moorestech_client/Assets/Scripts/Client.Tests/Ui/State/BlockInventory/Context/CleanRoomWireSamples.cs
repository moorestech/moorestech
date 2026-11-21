using System.Collections.Generic;
using Server.Protocol.PacketResponse;

namespace Client.Tests.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 部屋と確率の照会の型を組む。C7 で旧項目が外れたらこのファイルだけを直す
    /// Builds room and odds query packs; when C7 drops the old fields only this file changes
    /// </summary>
    public static class CleanRoomWireSamples
    {
        // 換気回数・体積・清浄機の一覧は C7 で消える旧項目。クライアントは読まないので空で渡す
        // Air change rate, volume and the purifier list are old fields C7 removes; the client reads none, so pass empties
        public static CleanRoomSummaryMessagePack ClassifiedRoom(string className, int thresholdIndex, int airFilterCount, int stoppedAirFilterCount, CleanRoomNextClassMessagePack nextClass, CleanRoomQueriedAirFilterMessagePack queriedAirFilter)
        {
            return CleanRoomSummaryMessagePack.CreateClassified(className, thresholdIndex, 0, 0, new List<CleanRoomAirFilterMessagePack>(), airFilterCount, stoppedAirFilterCount, nextClass, queriedAirFilter);
        }

        public static CleanRoomSummaryMessagePack OutRoom(int airFilterCount, int stoppedAirFilterCount, CleanRoomNextClassMessagePack nextClass, CleanRoomQueriedAirFilterMessagePack queriedAirFilter)
        {
            return CleanRoomSummaryMessagePack.CreateOut(0, 0, new List<CleanRoomAirFilterMessagePack>(), airFilterCount, stoppedAirFilterCount, nextClass, queriedAirFilter);
        }

        // 第1・第2引数（今の層の失敗率と出力待ちの抽選）は C7 で消える旧項目。クライアントは読まないので null
        // The first two arguments (current layer fail rate and the output-wait draw) are old fields C7 removes; the client reads neither, so null
        public static CleanRoomYieldLayerExposureView LayerExposure(double failedRate, List<CleanRoomYieldImprovementMessagePack> improvements)
        {
            return new CleanRoomYieldLayerExposureView(null, null, new LayerExposureOddsMessagePack(failedRate), improvements);
        }

        public static CleanRoomYieldDicingView Dicing(int outputCount, double[] levelRates, double failedRate, List<CleanRoomYieldImprovementMessagePack> improvements)
        {
            return new CleanRoomYieldDicingView(null, null, new DicingOddsMessagePack(outputCount, levelRates, failedRate), improvements);
        }
    }
}
