using Client.Game.InGame.UI.Tooltip;
using Client.Ui.State.CursorTooltip;

namespace Client.Tests.Ui.Screens.CursorTooltip
{
    // Web の mock-host の tooltip・tooltipMultiLine と同じ行の表示状態。テストは View.Render へ直に渡す
    // Display states with the same lines as the Web mock-host tooltip and tooltipMultiLine; tests pass them straight to View.Render
    public static class CursorTooltipViewModelSamples
    {
        public static CursorTooltipViewModel Lines(params TooltipLine[] lines) => new(lines);
    }
}
