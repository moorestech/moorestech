using Client.Ui.State.BlockInventory.Special.Details;

namespace Client.Ui.State.BlockInventory.Special.Selectors
{
    /// <summary>
    /// 特殊族の詳細の全体をそのまま渡す（本文・題の操作のように詳細の未着でも常に出す View 用）
    /// Passes the whole special detail through as is (for always-shown views such as the body and title action, even before the detail arrives)
    /// </summary>
    public sealed class WholeSpecialDetailSelector : ISpecialDetailSelector<SpecialBlockDetailViewModel>
    {
        public bool TryGet(SpecialBlockDetailViewModel detail, out SpecialBlockDetailViewModel value)
        {
            value = detail;
            return true;
        }
    }
}
