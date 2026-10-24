using System;
using System.Collections.Generic;
using System.Linq;

namespace Client.Game.InGame.UI.Tooltip
{
    /// <summary>
    ///     自分の所有者トークンで行をツールチップへ出す。自分の表示が立ったままで内容も同じなら Show を省き、Hide は自分の分だけ消す
    ///     Shows lines on the tooltip under its own owner token, skipping Show while its show stands with the same content, and hides only its own
    /// </summary>
    public class OwnedTooltipLinesWriter
    {
        private readonly IMouseCursorTooltip _tooltip;
        private readonly TooltipOwner _tooltipOwner = new();

        // 直近に自分が渡したスナップショット。内容が変わっていないかの比較に使う
        // The snapshot handed over last time, used to compare whether the content is unchanged
        private TooltipLine[] _lastShown = Array.Empty<TooltipLine>();

        public OwnedTooltipLinesWriter(IMouseCursorTooltip tooltip)
        {
            _tooltip = tooltip;
        }

        // 他の書き手が表示中か。譲るかどうかは呼び出し側が決める
        // Whether another writer is showing; the caller decides whether to yield
        private bool IsShownByOther()
        {
            var owner = _tooltip.CurrentOwner;
            return owner != null && !ReferenceEquals(owner, _tooltipOwner);
        }

        public bool TryShowWithoutReplacingOther(IReadOnlyList<TooltipLine> lines)
        {
            // silent-ok: 他の所有者の表示を優先する / 表示解放の通知で再提示する
            if (IsShownByOther()) return false;
            Show(lines);
            return true;
        }

        public void Show(IReadOnlyList<TooltipLine> lines)
        {
            // 毎フレーム呼ばれるため、自分の表示が出たままで内容も同じフレームは複製もShowも行わない
            // This runs every frame, so a frame whose content is unchanged and still ours skips both the copy and the Show
            if (IsUnchangedFromShown()) return;

            // TooltipPresentationは不変スナップショット前提のため、使い回しバッファではなく複製を渡す
            // TooltipPresentation assumes an immutable snapshot, so hand it a copy instead of the reused buffer
            _lastShown = lines.ToArray();
            _tooltip.Show(_tooltipOwner, _lastShown);

            #region Internal

            bool IsUnchangedFromShown()
            {
                // 所有者が自分でなければ他者が上書きしているので出し直す
                // If we are not the owner, someone else overwrote the tooltip and we must show again
                return ReferenceEquals(_tooltip.CurrentOwner, _tooltipOwner) && IsSameLines(lines, _lastShown);
            }


            // 毎フレーム走るためLINQ列挙子を作らず添字ループで比較する
            // Compares by index instead of LINQ enumerators because it runs every frame
            static bool IsSameLines(IReadOnlyList<TooltipLine> left, IReadOnlyList<TooltipLine> right)
            {
                if (left.Count != right.Count) return false;

                for (var lineIndex = 0; lineIndex < left.Count; lineIndex++)
                {
                    if (left[lineIndex].Key.Key != right[lineIndex].Key.Key) return false;

                    var leftParams = left[lineIndex].TextParams;
                    var rightParams = right[lineIndex].TextParams;
                    if (leftParams.Count != rightParams.Count) return false;

                    for (var paramIndex = 0; paramIndex < leftParams.Count; paramIndex++)
                    {
                        if (leftParams[paramIndex] != rightParams[paramIndex]) return false;
                    }
                }

                return true;
            }
            #endregion
        }

        public void Hide()
        {
            _lastShown = Array.Empty<TooltipLine>();
            _tooltip.Hide(_tooltipOwner);
        }


    }
}
