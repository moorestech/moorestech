using Client.Ui.State.BlockInventory.Special.Details;

namespace Client.Ui.State.BlockInventory.Special.Selectors
{
    /// <summary>
    /// 特殊族の詳細から区画の詳細を取り出す。取れたときだけ区画を出す（DC-35 案 A の「State が決める」を型で結ぶ）
    /// Selects a section's detail from the special detail; the section shows only when it is selected (DC-35 plan A's "State decides", tied by type)
    /// </summary>
    public interface ISpecialDetailSelector<TDetail>
    {
        bool TryGet(SpecialBlockDetailViewModel detail, out TDetail value);
    }
}
