using Client.Game.InGame.Train.View.Object.Core;

namespace Client.Game.InGame.UI.Inventory.Train
{
    /// <summary>
    /// 列車インベントリビューのインターフェース
    /// Train inventory view interface
    /// </summary>
    public interface ITrainInventoryView : ISubInventoryView
    {
        public void Initialize(TrainCarEntityObject trainCarEntity);
        public void HideSlotObjects();
        public void ShowMessage(TrainInventoryMessageType messageType);
    }

    public enum TrainInventoryMessageType
    {
        ContainerMissing,
        TrainCarMissing,
        OpenFailed
    }
}
