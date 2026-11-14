using Client.Tests.Ui.Components;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Theme;
using Client.Ui.Screens.Skit.Parts.Choices;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Skit
{
    /// <summary>
    /// 選択肢が choiceId で select を送り、会話窓の上の右端から下へ積まれ、押せる間だけ指のカーソルを出すことを確かめる（L1。Web SkitChoiceList）
    /// Verifies that choices send select by choiceId, stack upward from the right edge just above the window, and show the pointer cursor only while pressable (L1; Web SkitChoiceList)
    /// </summary>
    public class SkitChoiceListTest : ScreenTestHost
    {
        private RecordingSkitCommands _commands;
        private FakeUiOsCursorApplier _cursorApplier;
        private SkitChoiceList _choices;

        [SetUp]
        public void MountChoices()
        {
            _commands = new RecordingSkitCommands();
            _cursorApplier = new FakeUiOsCursorApplier();
            _choices = Mount(new SkitChoiceList(_commands, Toaster, new UiOsCursorRequestStack(_cursorApplier)));
            _choices.Render(SkitTestData.Choices(7, SkitTestData.ChoiceIntents));
            simulate.FrameUpdate();
        }

        [Test]
        public void 選択肢を押すとそのchoiceIdで選ぶ()
        {
            simulate.Click(Find<Button>(_choices, "skit-choice-route-b"));
            Assert.AreEqual(new SkitCommandRecord(SkitCommandKind.Select, SkitTestData.SessionId, 7, "route-b", false), _commands.Issued[0]);
        }

        [Test]
        public void 選択肢は窓の上へ右端から下から積む()
        {
            // 窓の高さ 204px＋0.7px の上、右 5.3px。最初の選択肢が一番下で、間隔は 14.7px
            // Above the 204px window plus 0.7px, 5.3px from the right; the first choice is lowest, 14.7px apart
            var routeA = Find<Button>(_choices, "skit-choice-route-a").worldBound;
            var routeB = Find<Button>(_choices, "skit-choice-route-b").worldBound;
            Assert.AreEqual(164f, routeA.width, 0.5f);
            Assert.AreEqual(30.7f, routeA.height, 0.5f);
            Assert.AreEqual(1280f - 5.3f, routeA.xMax, 0.5f);
            Assert.AreEqual(720f - 204f - 0.7f, routeA.yMax, 0.5f);
            Assert.AreEqual(routeA.yMin - 14.7f, routeB.yMax, 0.5f);
        }

        [Test]
        public void 板はボタンいっぱいに敷き左右だけフェードし押している間は面をシアン寄りにする()
        {
            // Web .choice は素の button で既定テーマのボタンの規則を受けず、板は .choice::before（GamePanel の variant ではない）
            // The Web .choice is a bare button free of the default theme's button rules, and its plate is .choice::before (not a GamePanel variant)
            var routeA = Find<Button>(_choices, "skit-choice-route-a");
            var plate = routeA.Q<GamePanelSurface>();
            Assert.IsFalse(routeA.ClassListContains(Button.ussClassName));
            Assert.IsTrue(plate.ClassListContains("game-panel-surface--hud"));
            Assert.AreEqual(routeA.worldBound.width, plate.worldBound.width, 0.5f);
            Assert.AreEqual(routeA.worldBound.height, plate.worldBound.height, 0.5f);
            Assert.AreEqual(routeA.worldBound.x, plate.worldBound.x, 0.5f);
            Assert.AreEqual(0f, plate.resolvedStyle.paddingLeft);
            var face = plate.Q(className: "game-panel-surface__face");
            AssertFace(face, "--gauge-track");
            foreach (var (knob, token) in new (string, string)[] { ("--game-panel-edge-fade", "--skit-choice-edge-fade"), ("--game-panel-top-fade", null), ("--game-panel-bottom-fade", null) })
            {
                Assert.IsTrue(UssCustomStyleReader.TryReadPixels(face.customStyle, knob, out var fade), knob);
                Assert.AreEqual(token == null ? 0f : ThemeTokenFile.ResolvePixels(token), fade, 0.0001f, knob);
            }

            simulate.MouseDown(routeA.worldBound.center);
            simulate.FrameUpdate();
            AssertFace(face, "--skit-choice-active-face");
            simulate.MouseUp(routeA.worldBound.center);
            simulate.FrameUpdate();
            AssertFace(face, "--gauge-track");

            #region Internal

            static void AssertFace(VisualElement faceElement, string token)
            {
                Assert.IsTrue(UssCustomStyleReader.TryReadColor(faceElement.customStyle, "--game-panel-face", out var color), token);
                ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor(token), color, token);
            }

            #endregion
        }

        [Test]
        public void 選択肢に乗ると指のカーソルを出しポーズ中は出さない()
        {
            var routeA = Find<Button>(_choices, "skit-choice-route-a");
            simulate.MouseMove(routeA.worldBound.center);
            Assert.AreEqual(UiOsCursorShape.Pointer, _cursorApplier.Current, "Web .choice の cursor: pointer");

            simulate.MouseMove(new UnityEngine.Vector2(5f, 5f));
            Assert.AreEqual(UiOsCursorShape.Default, _cursorApplier.Current, "離れたら戻る");

            // ポーズ中は拾わないので PointerEnter が来ず、指は出ない（Web の pointer-events: none）
            // While paused the buttons are not pickable, so no PointerEnter arrives and no finger shows (Web pointer-events: none)
            _choices.SetInteractive(false);
            simulate.MouseMove(routeA.worldBound.center);
            Assert.AreEqual(UiOsCursorShape.Default, _cursorApplier.Current);
        }
    }
}
