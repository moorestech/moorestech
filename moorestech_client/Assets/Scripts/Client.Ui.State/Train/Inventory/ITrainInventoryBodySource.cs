using UniRx;

namespace Client.Ui.State.Train.Inventory
{
    /// <summary>
    /// 開いている貨車の中身の口
    /// Source of the open car's contents
    /// </summary>
    public interface ITrainInventoryBodySource
    {
        IReadOnlyReactiveProperty<TrainInventoryBodyViewModel> Current { get; }
    }
}
