using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.Tooltip;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    ///     PlacementFeedback をカーソルツールチップへ反映する。行が無ければ自分が出した分だけ消す（DeleteObjectServiceと同じ規則）
    ///     Pushes PlacementFeedback into the cursor tooltip; with no lines it hides only what it showed itself (same rule as DeleteObjectService)
    /// </summary>
    public class PlacementFeedbackTooltipPresenter : IPlacementFeedbackPresenter
    {
        private readonly TooltipOwner _tooltipOwner = new();

        // 直近に自分が渡したスナップショット。表示中の配列と参照一致するかで所有権継続も判定する
        // The snapshot handed over last time; reference-matching it against the shown array also proves ownership continued
        private TooltipLine[] _lastShown = Array.Empty<TooltipLine>();

        public void Present(PlacementFeedback feedback)
        {
            // ツールチップはシーン配置のシングルトン。生成前に呼ばれるフレームは表示先が無いので何もしない（前例: UGuiTooltipTarget）
            // The tooltip is a scene-placed singleton; frames before it exists have no view, so do nothing (precedent: UGuiTooltipTarget)
            if (MouseCursorTooltip.Instance == null) return;

            if (feedback.Lines.Count == 0)
            {
                Hide();
                return;
            }

            // 毎フレーム呼ばれるため、自分の表示が出たままで内容も同じフレームは複製もShowも行わない
            // This runs every frame, so a frame whose content is unchanged and still ours skips both the copy and the Show
            if (IsUnchangedFromShown()) return;

            // TooltipPresentationは不変スナップショット前提のため、使い回しバッファではなく複製を渡す
            // TooltipPresentation assumes an immutable snapshot, so hand it a copy instead of the reused buffer
            _lastShown = feedback.Lines.ToArray();
            MouseCursorTooltip.Instance.Show(_tooltipOwner, _lastShown);

            #region Internal

            bool IsUnchangedFromShown()
            {
                // 表示中のスナップショットが自分の渡した配列そのものでなければ、他者が上書きしているので出し直す
                // If the shown snapshot is not the very array we handed over, someone else overwrote it and we must show again
                if (!ReferenceEquals(MouseCursorTooltip.Instance.GetPresentation().Lines, _lastShown)) return false;

                return IsSameLines(feedback.Lines, _lastShown);
            }

            #endregion
        }

        public void Hide()
        {
            _lastShown = Array.Empty<TooltipLine>();
            if (MouseCursorTooltip.Instance == null) return;

            MouseCursorTooltip.Instance.Hide(_tooltipOwner);
        }

        // 毎フレーム走るためLINQ列挙子を作らず添字ループで比較する
        // Compares by index instead of LINQ enumerators because it runs every frame
        private static bool IsSameLines(IReadOnlyList<TooltipLine> left, IReadOnlyList<TooltipLine> right)
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
    }
}
