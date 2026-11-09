using Client.Ui.Foundation.Layout;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.Text;

namespace Client.Ui.Screens.BlockInventory.Parts.Rows
{
    /// <summary>
    /// 稼働ラベルと電力率の組。Web は fragment なので、親の UiGapContainer へ 2 要素を直接足して親の間を保つ
    /// The state label paired with the power rate; the Web uses a fragment, so both elements go straight into the parent UiGapContainer to keep its gap
    /// </summary>
    public sealed class MachineStateRow
    {
        private readonly LackHighlightText _state;
        private readonly PowerRateText _powerRate;
        private UiGapContainer _parent;

        public MachineStateRow(string stateTestId, string powerRateTestId)
        {
            _state = new LackHighlightText(stateTestId, LackHighlightTextSize.Sm);
            _powerRate = new PowerRateText(powerRateTestId);
        }

        public void MountInto(UiGapContainer parent)
        {
            _parent = parent;
            parent.AddItem(_state);
            parent.AddItem(_powerRate);
        }

        public void Render(MachineProcessDisplayState state, double currentPower, double requestPower, bool highlightPowerShortage)
        {
            var display = BlockDetailStateDisplay.Machine(state);
            _state.Render(new UiText(display.LabelKey), display.Insufficient);
            // 隠すときも文は先に入れる（文無しの LocalizedLabel が載ると LogError）
            // Always set the text first, even when hiding (a mounted text-less LocalizedLabel logs an error)
            _powerRate.Render(currentPower, requestPower, highlightPowerShortage);
            _parent.SetItemShown(_powerRate, display.ShowPowerRate);
        }
    }
}
