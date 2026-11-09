using System.Text.RegularExpressions;
using Client.Ui.Screens.Tutorial.Anchor;
using Client.Ui.State.Tutorial.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Tutorial.Overlay
{
    /// <summary>
    /// アンカーの変化をそのフレームのうちに状態へ写すか（R-5「アンカーが消えたら同じフレームで枠も消す」）
    /// Whether anchor changes reach the state within the same frame (R-5: the frame disappears in the frame the anchor does)
    /// </summary>
    public class TutorialAnchorTrackerSameFrameTest : TutorialOverlayTestHost
    {
        [Test]
        public void アンカーを外したフレームで未発見になる()
        {
            var anchor = MountAnchor(rootVisualElement, "pause.save", 100, 50, 200, 40);
            var tracker = StartTrackingReady("pause.save");

            LogAssert.Expect(LogType.Warning, new Regex(@"\[TutorialAnchorTracker\] anchor is missing: pause\.save"));
            anchor.RemoveFromHierarchy();
            simulate.FrameUpdate();
            tracker.TryGet("pause.save", out var state);
            Assert.AreEqual(TutorialAnchorReason.Missing, state.Reason);
        }

        [Test]
        public void displayNoneにしたフレームで非表示になる()
        {
            var anchor = MountAnchor(rootVisualElement, "pause.save", 100, 50, 200, 40);
            var tracker = StartTrackingReady("pause.save");

            anchor.style.display = DisplayStyle.None;
            simulate.FrameUpdate();
            tracker.TryGet("pause.save", out var state);
            Assert.AreEqual(TutorialAnchorReason.DisplayNone, state.Reason);
        }

        [Test]
        public void 祖先をdisplayNoneにしたフレームで非表示になる()
        {
            var parent = new VisualElement { style = { position = Position.Absolute, left = 0, top = 0, width = 600, height = 400 } };
            rootVisualElement.Add(parent);
            MountAnchor(parent, "pause.save", 100, 50, 200, 40);
            var tracker = StartTrackingReady("pause.save");

            parent.style.display = DisplayStyle.None;
            simulate.FrameUpdate();
            tracker.TryGet("pause.save", out var state);
            Assert.AreEqual(TutorialAnchorReason.DisplayNone, state.Reason);
        }

        [Test]
        public void visibilityHiddenにしたフレームで非表示になる()
        {
            var anchor = MountAnchor(rootVisualElement, "pause.save", 100, 50, 200, 40);
            var tracker = StartTrackingReady("pause.save");

            anchor.style.visibility = Visibility.Hidden;
            simulate.FrameUpdate();
            tracker.TryGet("pause.save", out var state);
            Assert.AreEqual(TutorialAnchorReason.VisibilityHidden, state.Reason);
        }

        [Test]
        public void 動かしたフレームで矩形が追従する()
        {
            var anchor = MountAnchor(rootVisualElement, "pause.save", 100, 50, 200, 40);
            var tracker = StartTrackingReady("pause.save");

            // 動いた後の layout はレイアウト段で決まるので、スケジューラの測定だけでは次のフレームになる
            // The moved layout is settled in the layout phase, so the scheduler measure alone would land a frame late
            anchor.style.left = 300;
            simulate.FrameUpdate();
            tracker.TryGet("pause.save", out var state);
            Assert.AreEqual(new TutorialClipRect(300, 50, 500, 90), ((TutorialAnchorState.Ready)state).Rect);
        }

        [Test]
        public void 祖先を動かしたフレームで矩形が追従する()
        {
            var parent = new VisualElement { style = { position = Position.Absolute, left = 0, top = 0, width = 600, height = 400 } };
            rootVisualElement.Add(parent);
            MountAnchor(parent, "pause.save", 100, 50, 200, 40);
            var tracker = StartTrackingReady("pause.save");

            // アンカー自身の layout は変わらず worldBound だけが変わる
            // The anchor's own layout stays; only its worldBound changes
            parent.style.top = 100;
            simulate.FrameUpdate();
            tracker.TryGet("pause.save", out var state);
            Assert.AreEqual(new TutorialClipRect(100, 150, 300, 190), ((TutorialAnchorState.Ready)state).Rect);
        }

        private TutorialAnchorTracker StartTrackingReady(string anchorId)
        {
            var tracker = CreateTracker();
            tracker.SetSubscribedAnchors(Ids(anchorId));
            simulate.FrameUpdate();
            tracker.TryGet(anchorId, out var state);
            Assert.IsInstanceOf<TutorialAnchorState.Ready>(state);
            return tracker;
        }
    }
}
