using Client.Ui.State.BlockInventory.Special.Details;
using UniRx;

namespace Client.Ui.State.BlockInventory.Special
{
    // 開いたブロックの特殊族の詳細を配る口（View とテストの偽物が使う）
    // Port that publishes the open block's special-family details (used by views and test fakes)
    public interface ISpecialBlockDetailModel
    {
        IReadOnlyReactiveProperty<SpecialBlockDetailViewModel> Current { get; }
    }
}
