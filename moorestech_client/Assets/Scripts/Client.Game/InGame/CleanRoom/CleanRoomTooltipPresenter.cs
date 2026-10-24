using System.Collections.Generic;
using Client.Game.InGame.UI.Tooltip;

namespace Client.Game.InGame.CleanRoom
{
    /// <summary>
    ///     部屋の要約をカーソルツールチップへ出す。他の書き手が出している間は譲る
    ///     Shows the room summary on the cursor tooltip, yielding while another writer is showing
    /// </summary>
    public class CleanRoomTooltipPresenter
    {
        // 所有者トークン・同一内容の Show 省略・自分の分だけの Hide は、設置フィードバックと共通の書き手が持つ
        // The writer shared with placement feedback holds the owner token, the unchanged-content Show skip and the own-only Hide
        private readonly OwnedTooltipLinesWriter _writer;

        public CleanRoomTooltipPresenter(IMouseCursorTooltip tooltip)
        {
            _writer = new OwnedTooltipLinesWriter(tooltip);
        }

        // 毎フレーム呼ばれる。設置フィードバックとの違いは、他の書き手が出している間は譲る1行だけ
        // Called every frame; the only difference from placement feedback is the one line that yields to other writers
        public void Present(IReadOnlyList<TooltipLine> lines)
        {
            // silent-ok: インタラクト案内・設置フィードバックが優先する正常な調停で、相手が消えた次フレームに出し直す / 部屋の要約が出ないことは照準中の画面で見える
            if (_writer.IsShownByOther()) return;
            _writer.Show(lines);
        }

        public void Hide()
        {
            _writer.Hide();
        }
    }
}
