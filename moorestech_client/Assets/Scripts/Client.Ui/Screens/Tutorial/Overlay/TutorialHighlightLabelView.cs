using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Client.Ui.State.Tutorial.Logic;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.Overlay
{
    /// <summary>
    /// 枠に付くラベルの面。測った寸法で枠の下（収まらなければ上）へ置く（Web HighlightLabel）
    /// The label face attached to an outline; placed below the ring (or above when it does not fit) by its measured size (Web HighlightLabel)
    /// </summary>
    public sealed class TutorialHighlightLabelView : LocalizedLabel
    {
        private TutorialClipRect _box;
        private TutorialClipRect _clip;
        private float _gap;

        public TutorialHighlightLabelView()
        {
            name = "tutorial-highlight-label";
            AddToClassList("tutorial-highlight-label");
            pickingMode = PickingMode.Ignore;
            // 面の寸法は文と折り返し上限で決まるので測ってから置く。測るまでは見せない（Web は paint 前に確定）
            // The face size depends on text and wrap limit, so place after measuring; hide until then (Web settles before paint)
            style.visibility = Visibility.Hidden;
            RegisterCallback<GeometryChangedEvent>(_ => ApplyPlacement());
        }

        public void Place(UiText text, TutorialClipRect box, TutorialClipRect clip, float gap)
        {
            SetText(text);
            _box = box;
            _clip = clip;
            _gap = gap;
            style.maxWidth = WrapLimit();
            UiDisplay.SetShown(this, true);
            ApplyPlacement();
        }

        public void HideLabel()
        {
            // 木からは外さず隠すだけにする（載せ直しで同じ矩形だと GeometryChangedEvent が来ず、Visible に戻せなくなる）
            // Hide without removing from the tree (a re-attach at the same rect fires no GeometryChangedEvent, so it could never become Visible again)
            style.visibility = Visibility.Hidden;
            UiDisplay.SetShown(this, false);
        }

        // 置き場に載る前は器の幅で縛る
        // Before joining the holder, bound by the clip width
        private float WrapLimit()
        {
            var holderWidth = parent == null ? float.PositiveInfinity : parent.contentRect.width;
            return TutorialHighlightLabelPlacement.WrapLimit(_box, _clip, holderWidth);
        }

        private void ApplyPlacement()
        {
            // silent-ok: 木に無い・測る前（幅 0）は次の GeometryChangedEvent で置き直す / L1 TutorialOverlayLabelTest が位置を見る
            if (panel == null || resolvedStyle.display == DisplayStyle.None || layout.width <= 0) return;
            PinWidthWhenWrapped();
            var position = TutorialHighlightLabelPlacement.Place(_box, _clip, _gap, layout.width, layout.height);
            style.left = position.Left;
            style.top = position.Top;
            style.visibility = Visibility.Visible;

            #region Internal

            void PinWidthWhenWrapped()
            {
                var wrapLimit = WrapLimit();
                style.maxWidth = wrapLimit;
                // Unity は折り返した面を最長行の幅に縮めるが、Web は上限まで広がる。1 行の自然幅が内側を超えたら上限に固定する（前例 TooltipFace）
                // Unity shrinks a wrapped face to its longest line while Web fills the limit; pin the limit when the one-line width exceeds the inner width (precedent TooltipFace)
                var innerMaxWidth = wrapLimit - resolvedStyle.paddingLeft - resolvedStyle.paddingRight - resolvedStyle.borderLeftWidth - resolvedStyle.borderRightWidth;
                var naturalWidth = MeasureSingleLineWidth();
                style.width = innerMaxWidth < naturalWidth ? new StyleLength(wrapLimit) : new StyleLength(StyleKeyword.Auto);
            }

            #endregion
        }
    }
}
