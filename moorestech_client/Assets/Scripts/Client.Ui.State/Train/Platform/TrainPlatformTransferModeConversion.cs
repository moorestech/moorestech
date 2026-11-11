using Game.Block.Blocks.TrainRail;

namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// サーバーの移送モードとの写しを 1 か所に置く（読み手と送り手が共有する）
    /// Keeps the mapping to the server transfer mode in one place, shared by the reader and the sender
    /// </summary>
    public static class TrainPlatformTransferModeConversion
    {
        public static TrainPlatformTransferMode FromServer(TrainPlatformTransferComponent.TransferMode mode)
        {
            return mode == TrainPlatformTransferComponent.TransferMode.UnloadToPlatform ? TrainPlatformTransferMode.UnloadToPlatform : TrainPlatformTransferMode.LoadToTrain;
        }

        public static TrainPlatformTransferComponent.TransferMode ToServer(TrainPlatformTransferMode mode)
        {
            return mode == TrainPlatformTransferMode.UnloadToPlatform ? TrainPlatformTransferComponent.TransferMode.UnloadToPlatform : TrainPlatformTransferComponent.TransferMode.LoadToTrain;
        }
    }
}
