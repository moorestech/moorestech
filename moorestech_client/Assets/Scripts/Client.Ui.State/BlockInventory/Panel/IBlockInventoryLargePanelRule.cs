using System;
using UniRx;

namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// 大型パネル判定の寄与の口（Web isLargeMachinePanel を中立名で。どの blockType を大型にするかは寄与する側が持つ。D-16）
    /// Contribution port for the large-panel decision (Web isLargeMachinePanel under a neutral name; the contributor owns which block types go large, D-16)
    /// </summary>
    public interface IBlockInventoryLargePanelRule
    {
        // 開いているブロックを大型パネル（本文の高さ確定）にするか。聞かれるのはエラー無しのブロックだけ（D-60）
        // Whether the open block uses the large panel (with a fixed body height); asked only for error-free blocks (D-60)
        bool IsLargePanel(OpenBlockInventoryPanel panel);

        // 判定の入力（レシピの解放・選択など）が変わったら流す
        // Fires when the decision's inputs (recipe unlocks, selection, ...) change
        IObservable<Unit> OnRuleInputChanged { get; }
    }
}
