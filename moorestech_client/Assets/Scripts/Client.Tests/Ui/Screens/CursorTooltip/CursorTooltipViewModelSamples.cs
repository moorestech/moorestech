using Client.Game.InGame.UI.Tooltip;
using Client.Ui.State.CursorTooltip;

namespace Client.Tests.Ui.Screens.CursorTooltip
{
    // Web の tooltip 系と同じ行の表示状態
    // Display states with the same lines as the Web tooltip mocks
    public static class CursorTooltipViewModelSamples
    {
        public static CursorTooltipViewModel Lines(params TooltipLine[] lines) => new(lines);
    }
}
