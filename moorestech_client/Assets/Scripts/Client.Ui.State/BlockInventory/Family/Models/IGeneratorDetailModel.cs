using Client.Ui.State.BlockInventory.Common;
using UniRx;

namespace Client.Ui.State.BlockInventory.Family.Models
{
    /// <summary>
    /// 追跡ブロックの発電機の詳細（発電機でなければ Absent）
    /// The tracked block's generator detail (Absent when it is not a generator)
    /// </summary>
    public interface IGeneratorDetailModel
    {
        IReadOnlyReactiveProperty<SectionContent<GeneratorDetail>> Current { get; }
    }
}
