using System.Linq;
using System.Text.RegularExpressions;
using Client.Ui.Screens.Tutorial.Overlay;
using Client.Ui.State.Tutorial.Overlay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Tutorial.TutorialOverlayProjectionTest;

namespace Client.Tests.Ui.Screens.Tutorial.Overlay
{
    /// <summary>
    /// 枠のアンカー解決を確かめる（L1。Web TutorialOverlay.test.ts の anchor resolution。ack の 2 件は捨てる・D-10）
    /// Verifies anchor resolution of the overlay (L1; Web TutorialOverlay.test.ts anchor resolution; the two ack tests are dropped, D-10)
    /// </summary>
    public class TutorialOverlayAnchorTest : TutorialOverlayTestHost
    {
        private FakeTutorialOverlayModel _model;
        private TutorialOverlayView _view;

        [SetUp]
        public void MountOverlay()
        {
            _model = new FakeTutorialOverlayModel();
            _view = TutorialOverlayPresenter.Mount(rootVisualElement, rootVisualElement, _model.Current);
        }

        /// <web-test file="src/features/tutorial/overlay/TutorialOverlay.test.ts">revision更新でも購読が続くanchorの解決状態を保つ</web-test>
        [Test]
        public void revisionが変わっても続くアンカーの状態を保ち枠を出したままにする()
        {
            MountAnchor(rootVisualElement, "pause.save", 100, 100, 200, 40);
            _model.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "pause.save", null))));
            simulate.FrameUpdate();

            // フレームを回さずに revision だけを進めても、枠は前の解決結果で出たまま
            // Advancing only the revision without a frame keeps the outline drawn from the previous resolution
            _model.Set(TutorialOverlayProjection.Project(Data(2, Outline("o1", "pause.save", null))));
            var outline = _view.Query<TutorialOutlineView>().First();
            Assert.IsTrue(IsShown(outline.Q("tutorial-outline__clip")));
        }

        [Test]
        public void 画面を外すとアンカーの追跡を止める()
        {
            var anchor = MountAnchor(rootVisualElement, "pause.save", 100, 100, 200, 40);
            _model.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "pause.save", null))));
            simulate.FrameUpdate();

            // 画面を外した後にアンカーが消えても未発見の Warning は出ない（測定が止まっている。D-32）
            // After the screen is removed, a vanished anchor emits no missing Warning (measuring has stopped; D-32)
            _view.RemoveFromHierarchy();
            anchor.RemoveFromHierarchy();
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void アンカーを隠したフレームと外したフレームに枠を消す()
        {
            var anchor = MountAnchor(rootVisualElement, "pause.save", 100, 100, 200, 40);
            _model.Set(TutorialOverlayProjection.Project(Data(1, Outline("o1", "pause.save", null))));
            simulate.FrameUpdate();
            var clip = _view.Q(className: "tutorial-outline__clip");
            Assert.AreEqual(DisplayStyle.Flex, clip.style.display.value);

            // 1 フレームだけ回して、View が書いた表示の指定を直に見る（IsShown は余分にフレームを回すので使わない）
            // Run exactly one frame and read the View's inline display directly (IsShown would run an extra frame)
            anchor.style.display = DisplayStyle.None;
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, clip.style.display.value);

            anchor.style.display = DisplayStyle.Flex;
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.Flex, clip.style.display.value);

            LogAssert.Expect(LogType.Warning, new Regex(@"anchor is missing: pause\.save"));
            anchor.RemoveFromHierarchy();
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, clip.style.display.value);
        }
    }
}
