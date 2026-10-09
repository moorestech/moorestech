namespace Server.Protocol.PacketResponse.Util.Blueprint
{
    public readonly struct BlueprintPasteExecutionResult
    {
        public readonly int FailedLineCount;
        public readonly int CostShortageCopyCount;
        public readonly int PlacementFailedCopyCount;

        public BlueprintPasteExecutionResult(int failedLineCount, int costShortageCopyCount, int placementFailedCopyCount)
        {
            FailedLineCount = failedLineCount;
            CostShortageCopyCount = costShortageCopyCount;
            PlacementFailedCopyCount = placementFailedCopyCount;
        }
    }
}
