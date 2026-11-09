using System.Collections.Generic;
using System.Linq;
using Client.Ui.Foundation.Theme;
using Client.Ui.Screens.Tutorial.Pulse;
using Client.Ui.State.Tutorial.Logic;
using Client.Ui.State.Tutorial.Overlay;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.Overlay
{
    /// <summary>
    /// チュートリアルの枠を全面に重ねる画面。購読はせず、Presenter から Render と RedrawAnchor を受ける
    /// Full-screen layer of tutorial outlines; subscribes to nothing and receives Render and RedrawAnchor from the Presenter
    /// </summary>
    public sealed class TutorialOverlayView : VisualElement
    {
        private readonly ITutorialAnchorStateLookup _anchors;
        private readonly Dictionary<string, TutorialOutlineView> _outlines = new();
        private readonly TutorialPulseBinding _pulse;
        private TutorialOverlayViewModel _current = TutorialOverlayViewModel.Empty;
        private float _glowWidth;
        private float _labelGap;
        private float _ringRadius;

        public TutorialOverlayView(ITutorialAnchorStateLookup anchors)
        {
            name = "tutorial-overlay";
            AddToClassList("tutorial-overlay");
            pickingMode = PickingMode.Ignore;
            _anchors = anchors;
            _pulse = new TutorialPulseBinding(this);
            RegisterCallback<CustomStyleResolvedEvent>(evt => ReadKnobs(evt.customStyle));
        }

        public void Render(TutorialOverlayViewModel model)
        {
            _current = model;
            ReconcileOutlines(model);
            foreach (var outline in model.Outlines) Redraw(outline);
        }

        public void RedrawAnchor(string anchorId)
        {
            foreach (var outline in _current.Outlines)
            {
                if (outline.AnchorId == anchorId && _outlines.ContainsKey(outline.Key)) Redraw(outline);
            }
        }

        private void Redraw(TutorialOutlineCue cue)
        {
            var view = _outlines[cue.Key];
            // ready で、箱がクリップの内に残るときだけ枠を出す（ready 以外は仕様どおりの非表示で、理由は追跡器が遷移時に出す）
            // Show the ring only when ready and the box survives the clip (non-ready is a spec'd hide; the tracker reports reasons)
            if (!_anchors.TryGet(cue.AnchorId, out var state) || state is not TutorialAnchorState.Ready ready)
            {
                // silent-ok: 未解決・非表示のアンカーの枠は Web と同じく出さない仕様 / 理由は TutorialAnchorTracker が遷移時に Warning・LogError で出し、L1 TutorialOverlayAnchorTest が見る
                view.HideRing();
                return;
            }
            var box = ready.Rect.Expand(cue.PaddingPx);
            if (!TutorialHighlightClip.TryComputeInset(box, ready.Clip, _glowWidth, out var inset))
            {
                // silent-ok: 祖先クリップで完全に切れた枠は Web と同じく要素ごと出さない仕様 / L1 TutorialOverlayClipTest の「完全にクリップされた枠」が見る
                view.HideRing();
                return;
            }
            view.ShowRing(box, inset, _glowWidth, _ringRadius);
            // ラベルはアンカー自身がクリップに収まる時だけ出す（Web renderOutline）
            // Show the label only while the anchor itself fits the clip (Web renderOutline)
            if (cue is TutorialOutlineCue.Labeled labeled && TutorialHighlightLabelPlacement.ShowsLabel(ready.Rect, ready.Clip)) view.ShowLabel(labeled.Label, box, ready.Clip, _labelGap);
            else view.Label.HideLabel();
        }

        private void ReconcileOutlines(TutorialOverlayViewModel model)
        {
            // 消えた枠を外し、新しい枠だけ作る（続く枠は脈動の位相を保つ）
            // Remove vanished outlines and create only new ones (continuing outlines keep their pulse phase)
            var live = new HashSet<string>(model.Outlines.Select(outline => outline.Key));
            foreach (var key in _outlines.Keys.Where(key => !live.Contains(key)).ToList())
            {
                _outlines[key].RemoveFromHierarchy();
                _outlines.Remove(key);
            }
            foreach (var outline in model.Outlines)
            {
                if (_outlines.ContainsKey(outline.Key)) continue;
                var view = new TutorialOutlineView(_pulse);
                _outlines.Add(outline.Key, view);
                Add(view);
            }
        }

        private void ReadKnobs(ICustomStyle customStyle)
        {
            // 寸法はトークンを画面ルートで受け直した変数から読み、読めたら描き直す（読めなければ Reader が LogError）
            // Read sizes from the screen root's re-declared tokens and redraw (the reader logs an error when unreadable)
            UssCustomStyleReader.TryReadPixels(customStyle, "--tutorial-overlay-glow-width", out _glowWidth);
            UssCustomStyleReader.TryReadPixels(customStyle, "--tutorial-overlay-label-gap", out _labelGap);
            UssCustomStyleReader.TryReadPixels(customStyle, "--tutorial-overlay-ring-radius", out _ringRadius);
            Render(_current);
        }
    }
}
