namespace Client.Game.InGame.UI.Inventory.Train
{
    // 列車インベントリを開けなかった理由。UITK側のエラー文言キーへ写す
    // Why a train inventory could not be opened; mapped onto the UITK-side error key
    public enum TrainInventoryMessageType
    {
        ContainerMissing,
        TrainCarMissing,
        OpenFailed,
    }
}
