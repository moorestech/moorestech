using System.Text.RegularExpressions;
using Client.Ui.Foundation.Anchor;
using Client.Ui.State.Tutorial.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Tutorial.Overlay
{
    public class TutorialAnchorTrackerTest : TutorialOverlayTestHost
    {
        [Test]
        public void 載っているアンカーはreadyで矩形と画面のclipを持つ()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 50, 200, 40);
            var tracker = CreateTracker();
            tracker.SetSubscribedAnchors(Ids("pause.save"));
            simulate.FrameUpdate();

            Assert.IsTrue(tracker.TryGet("pause.save", out var state));
            Assert.IsInstanceOf<TutorialAnchorState.Ready>(state);
            var ready = (TutorialAnchorState.Ready)state;
            Assert.AreEqual(new TutorialClipRect(100, 50, 300, 90), ready.Rect);
            Assert.AreEqual(new TutorialClipRect(0, 0, 1280, 720), ready.Clip);
        }

        [Test]
        public void 重複は遷移した時に一度だけLogErrorを出す()
        {
            MountAnchor(rootVisualElement, "pause.save", 0, 0, 10, 10);
            MountAnchor(rootVisualElement, "pause.save", 20, 0, 10, 10);
            var tracker = CreateTracker();
            LogAssert.Expect(LogType.Error, new Regex(@"\[TutorialAnchorTracker\] anchor is duplicated \(2\): pause\.save"));
            tracker.SetSubscribedAnchors(Ids("pause.save"));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 未発見は遷移した時に一度だけ警告し毎フレームは出さない()
        {
            var tracker = CreateTracker();
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TutorialAnchorTracker\] anchor is missing: pause\.back"));
            tracker.SetSubscribedAnchors(Ids("pause.back"));
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 非表示と面積0と画面外は理由を分けて持つ()
        {
            var hidden = MountAnchor(rootVisualElement, "pause.save", 0, 0, 10, 10);
            hidden.style.display = DisplayStyle.None;
            MountAnchor(rootVisualElement, "pause.back", 0, 0, 0, 10);
            MountAnchor(rootVisualElement, "pause.settings", 2000, 0, 10, 10);
            var tracker = CreateTracker();
            tracker.SetSubscribedAnchors(Ids("pause.save", "pause.back", "pause.settings"));
            simulate.FrameUpdate();

            tracker.TryGet("pause.save", out var a);
            tracker.TryGet("pause.back", out var b);
            tracker.TryGet("pause.settings", out var c);
            Assert.AreEqual((TutorialAnchorReason.DisplayNone, TutorialAnchorReason.ZeroArea, TutorialAnchorReason.OutsideViewport), (a.Reason, b.Reason, c.Reason));
        }

        [Test]
        public void ScrollViewの内側のアンカーはviewportの矩形でclipされる()
        {
            var scroll = new ScrollView { style = { position = Position.Absolute, left = 0, top = 100, width = 400, height = 200 } };
            rootVisualElement.Add(scroll);
            var anchor = new VisualElement { style = { height = 40 } };
            TutorialAnchor.Mark(anchor, "research.node-a");
            scroll.Add(anchor);
            var tracker = CreateTracker();
            tracker.SetSubscribedAnchors(Ids("research.node-a"));
            simulate.FrameUpdate();

            tracker.TryGet("research.node-a", out var state);
            Assert.IsInstanceOf<TutorialAnchorState.Ready>(state);
            var ready = (TutorialAnchorState.Ready)state;
            Assert.AreEqual(100f, ready.Clip.Top);
            Assert.AreEqual(300f, ready.Clip.Bottom);
        }

        [Test]
        public void 印なしでインラインのoverflowだけを持つ祖先でもclipされる()
        {
            // 印も ScrollView も無く style.overflow = Hidden だけの祖先（R-2）
            // An ancestor with neither mark nor ScrollView, only inline overflow: hidden (R-2)
            var container = new VisualElement { style = { position = Position.Absolute, left = 50, top = 100, width = 300, height = 150, overflow = Overflow.Hidden } };
            rootVisualElement.Add(container);
            var anchor = new VisualElement { style = { height = 40 } };
            TutorialAnchor.Mark(anchor, "research.node-a");
            container.Add(anchor);
            var tracker = CreateTracker();
            tracker.SetSubscribedAnchors(Ids("research.node-a"));
            simulate.FrameUpdate();

            tracker.TryGet("research.node-a", out var state);
            Assert.IsInstanceOf<TutorialAnchorState.Ready>(state);
            var clip = ((TutorialAnchorState.Ready)state).Clip;
            Assert.AreEqual((50f, 100f, 350f, 250f), (clip.Left, clip.Top, clip.Right, clip.Bottom));
        }

        [Test]
        public void レイアウト前のアンカーはreadyにせず測れた次のフレームでreadyにする()
        {
            // 足した直後はまだレイアウトされず worldBound が NaN になりうる（NaN を矩形として通さない。D-33）
            // Right after Add the anchor is not laid out and worldBound can be NaN (never pass NaN as a rect; D-33)
            MountAnchor(rootVisualElement, "pause.save", 100, 50, 200, 40);
            var tracker = CreateTracker();
            tracker.SetSubscribedAnchors(Ids("pause.save"));
            Assert.IsTrue(tracker.TryGet("pause.save", out var before));
            Assert.IsInstanceOf<TutorialAnchorState.Hidden>(before);
            Assert.AreEqual(TutorialAnchorReason.ZeroArea, before.Reason);

            simulate.FrameUpdate();
            tracker.TryGet("pause.save", out var after);
            Assert.IsInstanceOf<TutorialAnchorState.Ready>(after);
        }

        [Test]
        public void 追跡を止めた後は測らず報告もしない()
        {
            var anchor = MountAnchor(rootVisualElement, "pause.save", 100, 50, 200, 40);
            var tracker = CreateTracker();
            tracker.SetSubscribedAnchors(Ids("pause.save"));
            simulate.FrameUpdate();

            // 止めた後にアンカーが消えても、未発見の Warning を出さず状態も残さない
            // After stopping, a vanished anchor yields no missing Warning and no kept state
            tracker.StopTracking();
            anchor.RemoveFromHierarchy();
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
            Assert.IsFalse(tracker.TryGet("pause.save", out _));
        }
    }
}
