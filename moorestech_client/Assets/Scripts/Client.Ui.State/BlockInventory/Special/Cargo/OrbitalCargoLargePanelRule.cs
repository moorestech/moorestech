using System;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;

namespace Client.Ui.State.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 積荷を選ぶ発射台と拠点を大型パネルにする（一覧に高さが要る。前例 RecipeMachineLargePanelRule）
    /// Makes the cargo-selecting launch pad and elevator base large panels; the list needs height (precedent RecipeMachineLargePanelRule)
    /// </summary>
    public sealed class OrbitalCargoLargePanelRule : IBlockInventoryLargePanelRule
    {
        // 判定は本文の種類だけで決まり、開いている間に変わらない
        // The decision depends only on the body kind and never changes while open
        public IObservable<Unit> OnRuleInputChanged => Observable.Never<Unit>();

        public bool IsLargePanel(OpenBlockInventoryPanel panel)
        {
            return panel.Components.Body == BlockInventoryBodyKind.RocketLaunchPad || panel.Components.Body == BlockInventoryBodyKind.SpaceElevator;
        }
    }
}
