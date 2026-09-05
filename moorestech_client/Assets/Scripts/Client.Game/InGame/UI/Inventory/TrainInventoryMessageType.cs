namespace Client.Game.InGame.UI.Inventory
{
    // 列車インベントリを開けなかった理由。Web側のエラー文言キーへ写す
    // Why a train inventory could not be opened; mapped onto the web-side error key
    public enum TrainInventoryMessageType
    {
        ContainerMissing,
        TrainCarMissing,
        OpenFailed,
    }
}
