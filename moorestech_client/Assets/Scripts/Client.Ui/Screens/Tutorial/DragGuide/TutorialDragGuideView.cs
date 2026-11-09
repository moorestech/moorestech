using Client.Ui.Foundation.Motion.Playback;
using Client.Ui.Foundation.Motion.Tween;
using Client.Ui.Foundation.Theme;
using Client.Ui.State.Tutorial.Logic;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.DragGuide
{
    /// <summary>
    /// 起点のアンカーから終点へ動くカーソル 1 つ（Web DragGuide の .dragGuide）
    /// One cursor travelling from the origin anchor to the target (Web DragGuide .dragGuide)
    /// </summary>
    public sealed class TutorialDragGuideView : VisualElement
    {
        private const string SizeProperty = "--tutorial-drag-guide-view-size";
        private const string DurationProperty = "--tutorial-drag-guide-view-duration";

        private readonly TutorialDragGuideStep _step;
        private TutorialDragGuidePlacement _placement;
        private UiMotionHandle _loop;
        private float _size;

        public TutorialDragGuideView()
        {
            name = "tutorial-drag-guide";
            AddToClassList("tutorial-drag-guide");
            pickingMode = PickingMode.Ignore;
            _step = new TutorialDragGuideStep(this);
            Add(new TutorialDragGuideGlyph());
            RegisterCallback<CustomStyleResolvedEvent>(evt => OnCustomStyleResolved(evt.customStyle));
            RegisterCallback<DetachFromPanelEvent>(_ => StopLoop());
            HideGuide();
        }

        public void Show(TutorialDragGuidePlacement placement)
        {
            _placement = placement;
            style.display = DisplayStyle.Flex;
            ApplyPlacement();
        }

        public void HideGuide()
        {
            style.display = DisplayStyle.None;
        }

        private void OnCustomStyleResolved(ICustomStyle customStyle)
        {
            // 寸法を読んだら置き直す（寸法 0 のまま置いた位置が半分ずれて残らないように）
            // Re-place once the size is read (so a placement made at size 0 does not stay off by half)
            if (UssCustomStyleReader.TryReadPixels(customStyle, SizeProperty, out _size))
            {
                style.width = _size;
                style.height = _size;
                ApplyPlacement();
            }
            // 周期が読めたら 1 周のループを 1 回だけ始める（外れたら止め、載せ直しの解決で始め直す）
            // Start the loop once the duration is read (stopped on detach, restarted on the re-attach resolution)
            if (_loop == null && UssCustomStyleReader.TryReadMilliseconds(customStyle, DurationProperty, out var duration))
            {
                _loop = UiMotionPlayer.Play(this, new UiTweenAnimation(new UiMotionSpec(duration, 0f, UiEasing.Linear, UiMotionLoop.Repeat), _step));
            }
        }

        private void ApplyPlacement()
        {
            // silent-ok: 寸法の解決前は CustomStyleResolvedEvent が置き直す / L1 両端が最初から揃っていても寸法の解決後に中心へ置く が見る
            if (_size <= 0f) return;
            // 中心を起点に合わせる（Web の負の margin）
            // Center on the origin (Web's negative margin)
            style.left = _placement.OriginX - _size / 2f;
            style.top = _placement.OriginY - _size / 2f;
            _step.SetTravel(_placement.TravelX, _placement.TravelY);
        }

        private void StopLoop()
        {
            // silent-ok: 一度も始まっていなければ止めるものが無い / 始まりは OnCustomStyleResolved が担う
            if (_loop == null) return;
            _loop.Cancel();
            _loop = null;
        }
    }
}
