using System.Collections.Generic;

namespace Server.Protocol.PacketResponse.Util.Blueprint
{
    public readonly struct BlueprintPasteExecutionResult
    {
        public readonly int FailedLineCount;
        public readonly int CostShortageCopyCount;
        public readonly int PlacementFailedCopyCount;
        public readonly bool HasCostShortage;
        public readonly List<BlueprintPlacedCellMessagePack> PlacedCells;

        internal BlueprintPasteExecutionResult(int failedLineCount, int costShortageCopyCount,
            int placementFailedCopyCount, bool hasCostShortage, List<BlueprintPlacedCellMessagePack> placedCells)
        {
            FailedLineCount = failedLineCount;
            CostShortageCopyCount = costShortageCopyCount;
            PlacementFailedCopyCount = placementFailedCopyCount;
            HasCostShortage = hasCostShortage;
            PlacedCells = placedCells;
        }
    }
}
