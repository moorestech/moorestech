using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Parts.Rows;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の区画の共通フッタ
    /// Shared footer (Web MachineSection footer): state label and power rate, plus the reason only on output-space shortage
    /// </summary>
    public sealed class MachineSectionFooter : VisualElement
    {
        private readonly UiGroup _row = new(UiGap.Xs);
        private readonly MachineStateRow _stateRow = new("machine-state-label", "machine-power-rate");
        private readonly LackHighlightText _waitReason = new("machine-wait-reason", LackHighlightTextSize.Sm);

        public MachineSectionFooter()
        {
            pickingMode = PickingMode.Ignore;
            _row.pickingMode = PickingMode.Ignore;
            _row.AddToClassList(UiLayoutClass.GroupJustifyCenter);
            _stateRow.MountInto(_row);
            _row.AddItem(_waitReason);
            Add(_row);

            // 機械でないブロックでも載るので、文の無いラベルが LogError にならないよう文を先に入れる
            // This is mounted on non-machine blocks too, so seed the texts to keep text-less labels from logging an error
            _stateRow.Render(MachineProcessDisplayState.Idle, 0, 0, false);
            _waitReason.Render(new UiText(LocalizationKeys.Ui.BlockInventory.MachineWaitOutputSpaceShort), true);
            _row.SetItemShown(_waitReason, false);
        }

        public void Render(MachineDetail detail)
        {
            // 歯車駆動の不足赤は網の停止理由行が担うので、充足率の赤は電力駆動だけ（ADR 0056）
            // Gear-driven shortage red belongs to the network stop-reason row, so only electric drive reddens the rate (ADR 0056)
            _stateRow.Render(detail.State, detail.CurrentPower, detail.RequestPower, !detail.GearDriven);

            // 出力空き不足のときだけ理由を出す
            // Show the reason only on output-space shortage
            _row.SetItemShown(_waitReason, detail.OutputSpaceShort);
        }
    }
}
