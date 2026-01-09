using Client.Game.InGame.Entity.Object;

namespace Client.Game.InGame.UI.Inventory.Train
{
    /// <summary>
    /// 列車インベントリビューのインターフェース
    /// Train inventory view interface
    /// </summary>
    public interface ITrainInventoryView : ISubInventoryView
    {
        public void Initialize(TrainCarEntityObject trainCarEntity);
    }
}