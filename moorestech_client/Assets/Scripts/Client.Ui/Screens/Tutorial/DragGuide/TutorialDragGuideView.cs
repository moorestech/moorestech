using Client.Ui.Foundation.Layout;
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
        private float _duration;
        private bool _shown;
        private bool _sizeResolved;
        private bool _durationResolved;

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
            UiDisplay.SetShown(this, true);
            ApplyPlacement();
            // 隠れていた後の表示は 1 周の始めから流す（Web は出し直しで keyframes が 0% から始まる）
            // After being hidden, start the loop from its beginning (Web restarts the keyframes at 0% on re-show)
            if (_shown) return;
            _shown = true;
            StartLoop();
        }

        public void HideGuide()
        {
            _shown = false;
            UiDisplay.SetShown(this, false);
            StopLoop();
        }

        private void OnCustomStyleResolved(ICustomStyle customStyle)
        {
            // 寸法を読んだら置き直す（寸法 0 のまま置いた位置が半分ずれて残らないように）
            // Re-place once the size is read (so a placement made at size 0 does not stay off by half)
            _sizeResolved = UssCustomStyleReader.TryReadPixels(customStyle, SizeProperty, out _size);
            if (_sizeResolved)
            {
                style.width = _size;
                style.height = _size;
                ApplyPlacement();
            }
            // 周期が読めたら、出ている間だけループを始める（外れたら止め、載せ直しの解決で始め直す）
            // Once the duration is read, run the loop only while shown (stopped on detach, restarted on the re-attach resolution)
            _durationResolved = UssCustomStyleReader.TryReadMilliseconds(customStyle, DurationProperty, out _duration);
            if (_durationResolved && _shown && _loop == null) StartLoop();
        }

        private void StartLoop()
        {
            StopLoop();
            // silent-ok: 周期の解決前は OnCustomStyleResolved が始める / 読めなければ UssCustomStyleReader が LogError
            if (!_durationResolved) return;
            // silent-ok: パネルから外れている間は始めない（取消済みハンドルが残ると載せ直しで始め直せない） / 載せ直しの解決が始める
            if (panel == null) return;
            // 最初の更新までの 1 フレームに前の周の位置が残らないよう、始めの姿（透明・起点）を先に当てる
            // Apply the starting pose (transparent, at the origin) first so the previous loop's pose does not linger for a frame
            _step.Apply(0f);
            _loop = UiMotionPlayer.Play(this, new UiTweenAnimation(new UiMotionSpec(_duration, 0f, UiEasing.Linear, UiMotionLoop.Repeat), _step));
        }

        private void ApplyPlacement()
        {
            // silent-ok: 寸法の解決前は CustomStyleResolvedEvent が置き直す / L1 両端が最初から揃っていても寸法の解決後に中心へ置く が見る
            if (!_sizeResolved) return;
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
