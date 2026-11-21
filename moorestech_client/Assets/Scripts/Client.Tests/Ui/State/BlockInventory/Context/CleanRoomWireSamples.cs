using System.Collections.Generic;
using Server.Protocol.PacketResponse;

namespace Client.Tests.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 部屋と確率の照会の型を組む
    /// Builds room and odds query packs
    /// </summary>
    public static class CleanRoomWireSamples
    {
        public static CleanRoomSummaryMessagePack ClassifiedRoom(string className, int thresholdIndex, int airFilterCount, int stoppedAirFilterCount, CleanRoomNextClassMessagePack nextClass, CleanRoomQueriedAirFilterMessagePack queriedAirFilter)
        {
            return CleanRoomSummaryMessagePack.CreateClassified(className, thresholdIndex, airFilterCount, stoppedAirFilterCount, nextClass, queriedAirFilter);
        }

        public static CleanRoomSummaryMessagePack OutRoom(int airFilterCount, int stoppedAirFilterCount, CleanRoomNextClassMessagePack nextClass, CleanRoomQueriedAirFilterMessagePack queriedAirFilter)
        {
            return CleanRoomSummaryMessagePack.CreateOut(airFilterCount, stoppedAirFilterCount, nextClass, queriedAirFilter);
        }

        public static CleanRoomYieldLayerExposureView LayerExposure(double failedRate, List<CleanRoomYieldImprovementMessagePack> improvements)
        {
            return new CleanRoomYieldLayerExposureView(new LayerExposureOddsMessagePack(failedRate), improvements);
        }

        public static CleanRoomYieldDicingView Dicing(int outputCount, double[] levelRates, double failedRate, List<CleanRoomYieldImprovementMessagePack> improvements)
        {
            return new CleanRoomYieldDicingView(new DicingOddsMessagePack(outputCount, levelRates, failedRate), improvements);
        }
    }
}
