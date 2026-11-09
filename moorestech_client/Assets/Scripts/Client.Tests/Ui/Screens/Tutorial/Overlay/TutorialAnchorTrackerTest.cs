using System.Text.RegularExpressions;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Motion.Steps;
using Client.Ui.Screens.Tutorial.Pulse;
using Client.Ui.State.Tutorial.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Tutorial.Overlay
{
    public class TutorialAnchorTrackerTest : TutorialOverlayTestHost
    {
        private void LayOutAndMeasure()
        {
            // 追跡のポーリングはそのフレームのレイアウトより前に走るので、レイアウト後の測定は次のフレーム
            // The poll runs before that frame's layout, so the post-layout measurement lands on the next frame
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        [Test]
        public void 載っているアンカーはreadyで矩形と画面のclipを持つ()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 50, 200, 40);
            var tracker = CreateTracker();
            tracker.SetSubscribedAnchors(Ids("pause.save"));
            LayOutAndMeasure();

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
            LayOutAndMeasure();

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
            LayOutAndMeasure();

            tracker.TryGet("research.node-a", out var state);
            Assert.IsInstanceOf<TutorialAnchorState.Ready>(state);
            var ready = (TutorialAnchorState.Ready)state;
            Assert.AreEqual(100f, ready.Clip.Top);
            Assert.AreEqual(300f, ready.Clip.Bottom);
        }

        [Test]
        public void 脈動は周期の半分で頂点に達し元へ戻る()
        {
            var target = new VisualElement();
            rootVisualElement.Add(target);
            var handle = TutorialAttentionPulse.Start(target, TutorialAttentionPulse.StrongScale, 1200f);
            // UiMotionHandle は Play の次のスケジューラ呼び出しを開始時刻にする（前例 UiMotionPlayerTest）
            // UiMotionHandle starts at the next scheduler call after Play (precedent UiMotionPlayerTest)
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(600);
            Assert.AreEqual(1.08f, target.resolvedStyle.scale.value.x, 0.005f);
            simulate.FrameUpdateMs(600);
            Assert.AreEqual(1f, target.resolvedStyle.scale.value.x, 0.005f);
            handle.Cancel();
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
