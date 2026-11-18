using Client.Ui.Screens.BlockInventory.Registry;
using UniRx;

namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文の 1 区画の Presenter。描くかどうかは区画が自分の Model から決めて通知する（Web の各 Section が null を返す条件）
    /// Presenter of one standard-body section; the section decides from its own model whether it draws and signals it (the condition each Web Section returns null on)
    /// </summary>
    public interface IBlockInventorySection : IBlockInventoryPart
    {
        // Attach 後に有効。パネルの組み直しの外（網の集約・ベルトの統計）で変わってよく、SectionStackBody が購読して出し入れと間隔を組み直す
        // Valid after Attach; may change outside panel rebuilds (network totals, belt stats); SectionStackBody subscribes to re-lay visibility and gaps
        IReadOnlyReactiveProperty<bool> IsApplicable { get; }
    }
}
