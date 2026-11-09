using Client.Ui.Foundation.Input;
using Client.Ui.Screens.Tutorial.Pulse;
using Client.Ui.State.Text;
using Client.Ui.State.Tutorial.Logic;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.Overlay
{
    /// <summary>
    /// 強調枠 1 つ分（グロー・赤い線・祖先クリップの切り口）とそのラベル（Web TutorialOverlay の .highlight）
    /// One outline (glow, red ring and ancestor-clip cut) with its label (Web TutorialOverlay .highlight)
    /// </summary>
    public sealed class TutorialOutlineView : VisualElement
    {
        public readonly TutorialHighlightLabelView Label = new();
        private readonly VisualElement _clip = new() { name = "tutorial-outline__clip" };
        private readonly VisualElement _glow = new();
        private readonly VisualElement _ring = new();
        private readonly TutorialPulseBinding _pulse;

        public TutorialOutlineView(TutorialPulseBinding pulse)
        {
            _pulse = pulse;
            name = "tutorial-outline";
            AddToClassList("tutorial-outline");
            _clip.AddToClassList("tutorial-outline__clip");
            _glow.AddToClassList("tutorial-outline__glow");
            _ring.AddToClassList("tutorial-outline__ring");
            _clip.Add(_glow);
            _clip.Add(_ring);
            Add(_clip);
            // 枠は入力を奪わない（ラベルは自分で拾わなくする）
            // The outline never takes input (the label turns its own picking off)
            UiPickingIgnore.ApplyToSubtree(this);
            // ラベルは文を当ててから木に載せる（文の無い LocalizedLabel は 1 フレーム後に LogError。mode-tooltip D-28 と同形）
            // Attach the label only after its text is set (a text-less LocalizedLabel logs an error a frame later; same as mode-tooltip D-28)
            // 枠は 1.03 で脈動し、ラベルは脈動しない（Web tutorialAttentionDesign）
            // The ring pulses at 1.03 and the label does not (Web tutorialAttentionDesign)
            pulse.Attach(_clip, TutorialAttentionPulse.SubtleScale);
            HideRing();
        }

        public void Discard()
        {
            // 木から外し、clip を脈動の管理辞書から外す（Attach のコールバックは要素に残るが Start は管理外を流さない）
            // Remove from the tree and drop the clip from the pulse dictionary (Attach callbacks stay on the element but Start never pulses an untracked target)
            RemoveFromHierarchy();
            _pulse.Detach(_clip);
        }

        public void ShowRing(TutorialClipRect box, TutorialClipInset inset, float glowWidth, float ringRadius, bool glowOverWorld)
        {
            // 容器は箱を inset で縮めた矩形。負の inset は外へ広げてグローを残す（Web clipPathInset）
            // The container is the box shrunk by the inset; negative insets widen it to keep the glow (Web clipPathInset)
            var left = box.Left + inset.Left;
            var top = box.Top + inset.Top;
            SetRect(_clip, left, top, box.Right - inset.Right - left, box.Bottom - inset.Bottom - top);
            SetRect(_ring, box.Left - left, box.Top - top, box.Width, box.Height);
            SetRect(_glow, box.Left - left - glowWidth, box.Top - top - glowWidth, box.Width + glowWidth * 2f, box.Height + glowWidth * 2f);
            SetBorder(_glow, glowWidth, ringRadius + glowWidth);
            // 世界の地の上では明るい地に合わせた α に替える（1 つの α では暗い面と明るい地を両立できない。LX-38）
            // Over the world switch to the alpha fitted to bright ground (one alpha cannot serve dark surfaces and bright ground; LX-38)
            _glow.EnableInClassList("tutorial-outline__glow--over-world", glowOverWorld);
            // 拡縮の中心は箱の中心（Web は .highlight 自身の中心）
            // Scale around the box center (Web scales .highlight around its own center)
            _clip.style.transformOrigin = new TransformOrigin(new Length(box.CenterX - left), new Length(box.CenterY - top));
            _clip.style.display = DisplayStyle.Flex;

            #region Internal

            void SetRect(VisualElement element, float x, float y, float width, float height)
            {
                element.style.left = x;
                element.style.top = y;
                element.style.width = width;
                element.style.height = height;
            }

            void SetBorder(VisualElement element, float width, float radius)
            {
                element.style.borderTopWidth = width;
                element.style.borderRightWidth = width;
                element.style.borderBottomWidth = width;
                element.style.borderLeftWidth = width;
                element.style.borderTopLeftRadius = radius;
                element.style.borderTopRightRadius = radius;
                element.style.borderBottomLeftRadius = radius;
                element.style.borderBottomRightRadius = radius;
            }

            #endregion
        }

        public void ShowLabel(UiText text, TutorialClipRect box, TutorialClipRect clip, float gap)
        {
            Label.Place(text, box, clip, gap);
            if (Label.parent == null) Add(Label);
        }

        public void HideRing()
        {
            _clip.style.display = DisplayStyle.None;
            Label.HideLabel();
        }
    }
}
