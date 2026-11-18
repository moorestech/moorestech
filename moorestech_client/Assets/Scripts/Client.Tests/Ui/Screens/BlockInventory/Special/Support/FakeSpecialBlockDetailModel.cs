using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Details;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Support
{
    // 特殊族の詳細の偽の Model（テストが Set で詳細を差し込む）
    // Fake special-detail model; the test pushes details with Set
    public sealed class FakeSpecialBlockDetailModel : FakeModel<SpecialBlockDetailViewModel>, ISpecialBlockDetailModel
    {
        public FakeSpecialBlockDetailModel() : base(SpecialBlockDetailViewModel.Empty)
        {
        }
    }
}
