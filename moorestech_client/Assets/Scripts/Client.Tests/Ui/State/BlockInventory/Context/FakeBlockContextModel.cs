using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Context;
using UniRx;

namespace Client.Tests.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 文脈の 3 欄 Model の偽物。Set で値を差し替える
    /// Fake of the context-entries model; swap values with Set
    /// </summary>
    public sealed class FakeBlockContextModel : FakeModel<BlockContextViewModel>, IBlockContextModel
    {
        public FakeBlockContextModel() : base(BlockContextViewModel.None)
        {
        }
    }
}
