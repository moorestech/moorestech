using Client.Ui.State.BlockInventory.Common;
using UniRx;

namespace Client.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// 開いた機械の区画の値の源
    /// Source of the open machine section's values
    /// </summary>
    public interface IMachineSectionModel
    {
        IReadOnlyReactiveProperty<SectionContent<MachineSectionViewModel>> Current { get; }
    }
}
