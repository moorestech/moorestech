namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// 積込（PF → 列車）と卸し（列車 → PF）。View が Game.Block を参照しないよう State で持つ（D-8）
    /// Load (platform to train) and unload (train to platform); held in State so the View never references Game.Block (D-8)
    /// </summary>
    public enum TrainPlatformTransferMode
    {
        LoadToTrain,
        UnloadToPlatform,
    }
}
