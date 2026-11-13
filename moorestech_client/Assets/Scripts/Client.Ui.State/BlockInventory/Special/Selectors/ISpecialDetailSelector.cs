using Client.Ui.State.BlockInventory.Special.Details;

namespace Client.Ui.State.BlockInventory.Special.Selectors
{
    /// <summary>
    /// 特殊族の詳細から本文・題の操作・区画が描く詳細を取り出す。取れたときだけ出す（DC-35 案 A の「State が決める」を型で結ぶ。全体は WholeSpecialDetailSelector）
    /// Selects the detail a special body, title action or section draws; it shows only when selected (DC-35 plan A's "State decides", tied by type; the whole detail uses WholeSpecialDetailSelector)
    /// </summary>
    public interface ISpecialDetailSelector<TDetail>
    {
        bool TryGet(SpecialBlockDetailViewModel detail, out TDetail value);
    }
}
