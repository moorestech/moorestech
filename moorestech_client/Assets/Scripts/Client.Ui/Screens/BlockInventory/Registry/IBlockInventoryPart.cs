using Client.Ui.State.BlockInventory.Panel;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Registry
{
    /// <summary>
    /// パネルへ差し込む部分の Presenter（本文・題の操作・区画）。Root が View。Attach で自分の Model を購読して View へ流し、Detach で切る
    /// Presenter of a part plugged into the panel (body, title action, section); Root is the view. Attach subscribes its own model into the view, Detach unsubscribes
    /// </summary>
    public interface IBlockInventoryPart
    {
        VisualElement Root { get; }

        // 同じパネル（BlockInventoryPanelState.IsSamePanel）のあいだは 1 回だけ。別のパネルでは Detach の後に新しい部分で呼ばれる
        // Called once per same panel (BlockInventoryPanelState.IsSamePanel); a different panel gets a fresh part after Detach
        void Attach(OpenBlockInventoryPanel panel);
        void Detach();
    }
}
