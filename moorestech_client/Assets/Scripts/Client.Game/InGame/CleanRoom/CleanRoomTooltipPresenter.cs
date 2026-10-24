using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.Tooltip;

namespace Client.Game.InGame.CleanRoom
{
    /// <summary>
    ///     部屋の要約をカーソルツールチップの補足行へ出す。ハッチの操作案内など他の書き手の表示があればその下に併記する
    ///     Shows the room summary as the cursor tooltip's supplement, listed under another writer's show such as a hatch's interact hint
    /// </summary>
    public class CleanRoomTooltipPresenter
    {
        private readonly IMouseCursorTooltip _tooltip;
        private readonly TooltipOwner _tooltipOwner = new();

        public CleanRoomTooltipPresenter(IMouseCursorTooltip tooltip)
        {
            _tooltip = tooltip;
        }

        public void Present(IReadOnlyList<TooltipLine> lines)
        {
            // 表示は不変スナップショット前提のため、呼び出し側の配列を複製して渡す
            // The presentation assumes an immutable snapshot, so hand over a copy of the caller's array
            _tooltip.ShowSupplement(_tooltipOwner, lines.ToArray());
        }

        public void Hide()
        {
            _tooltip.HideSupplement(_tooltipOwner);
        }
    }
}
