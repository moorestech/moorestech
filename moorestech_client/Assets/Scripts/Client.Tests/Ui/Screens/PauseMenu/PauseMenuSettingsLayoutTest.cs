using System.Linq;
using Client.Game.InGame.GraphicsQuality;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Input;
using Client.Localization;
using Client.Tests.Ui.Screens.Settings;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.PauseMenu;
using Client.Ui.State.PauseMenu;
using Client.Ui.State.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.PauseMenu
{
    /// <summary>
    /// Web の pauseMenuSettingsLayout.spec.ts を 2 つの論理解像度で写す。2432x786 は Expand（参照 1280x720）で 2227.7x720 になる
    /// Copies Web pauseMenuSettingsLayout.spec.ts at two logical sizes; 2432x786 becomes 2227.7x720 under Expand (reference 1280x720)
    /// </summary>
    [TestFixture(1280f, 720f)]
    [TestFixture(2227.7f, 720f)]
    public class PauseMenuSettingsLayoutTest : ScreenTestHost
    {
        private readonly Vector2 _logicalSize;
        private FakePauseMenuModel _model;
        private FakeSettingsModel _settings;
        private RecordingPauseMenuCommands _commands;
        private RecordingSettingsCommands _settingsCommands;
        private PauseMenuView _view;

        public PauseMenuSettingsLayoutTest(float width, float height)
        {
            _logicalSize = new Vector2(width, height);
        }

        [OneTimeSetUp]
        public void UseLogicalSize()
        {
            // 基底の OneTimeSetUp（本番の 1280x720）の後に走り、パネルの寸法だけを差し替える
            // Runs after the base OneTimeSetUp (production 1280x720) and only swaps the panel size
            panelSize = _logicalSize;
        }

        [SetUp]
        public void MountPauseMenu()
        {
            _model = new FakePauseMenuModel(FakePauseMenuModel.Page(PauseMenuPage.Top));
            _settings = new FakeSettingsModel(FakeSettingsModel.Default());
            _commands = new RecordingPauseMenuCommands();
            _settingsCommands = new RecordingSettingsCommands();
            _view = Mount(new PauseMenuView(_model.Current, _settings.Current, _commands, _settingsCommands, Toasts, Toaster, new BugReportDraft(), new UiOsCursorRequestStack(new FakeUiOsCursorApplier())));
            _view.SetShown(true);
            simulate.FrameUpdate();
        }

        /// <web-test file="e2e/tests/system/pauseMenuSettingsLayout.spec.ts">設定画面は${viewport.width}x${viewport.height}で枠内に収まり本文だけスクロールする</web-test>
        [Test]
        public void 設定画面は枠内に収まり本文だけスクロールし末尾の項目と戻るを押せる()
        {
            ShowSettings();
            var frame = Find<VisualElement>(_view, "pause-menu").worldBound;
            Assert.GreaterOrEqual(frame.y, 0f);
            Assert.LessOrEqual(frame.yMax, _logicalSize.y + 0.5f);

            var body = Find<GameScrollView>(_view, "pause-menu-body");
            Assert.Greater(body.contentContainer.layout.height, body.contentViewport.layout.height);
            var back = Find<Button>(_view, "pause-menu-back");
            Assert.LessOrEqual(back.worldBound.yMax, frame.yMax + 0.5f);

            // 末尾の項目までスクロールして押し、戻るで top へ戻す
            // Scroll to the last item, press it, then go back to top
            var oceanOff = Find<VisualElement>(_view, "graphics-ocean-off");
            body.ScrollTo(oceanOff);
            Settle();
            AssertPickable(oceanOff);
            simulate.Click(oceanOff);
            var record = _settingsCommands.Issued.Last();
            Assert.AreEqual(SettingsCommandKind.SetGraphicsQuality, record.Kind);
            Assert.IsFalse(record.Graphics.OceanVisible);

            simulate.Click(back);
            Assert.AreEqual(PauseMenuCommandKind.ShowPage, _commands.Issued.Last().Kind);
            Assert.AreEqual(PauseMenuPage.Top, _commands.Issued.Last().Page);
        }

        /// <web-test file="e2e/tests/system/pauseMenuSettingsLayout.spec.ts">復旧表示は${viewport.width}x${viewport.height}で収まり同値操作と解除通知を受ける</web-test>
        [Test]
        public void 復旧表示は枠内に収まり同値操作を送り解除通知で消える()
        {
            Localize.TrySetChosenLanguage("japanese");
            _settings.Set(new SettingsViewModel(GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium), GraphicsQualityPresetChoice.Medium, true, SprintInputMode.Toggle, false, "japanese"));
            ShowSettings();

            var body = Find<GameScrollView>(_view, "pause-menu-body");
            var notice = Find<Label>(_view, "graphics-recovered");
            body.ScrollTo(notice);
            Settle();
            StringAssert.Contains("画質設定を中に復旧しました", notice.text);
            var frame = Find<VisualElement>(_view, "pause-menu").worldBound;
            Assert.GreaterOrEqual(notice.worldBound.x, frame.x - 0.5f);
            Assert.LessOrEqual(notice.worldBound.xMax, frame.xMax + 0.5f);
            Assert.LessOrEqual(notice.worldBound.yMax, frame.yMax + 0.5f);
            Assert.LessOrEqual(notice.worldBound.yMax, body.contentViewport.worldBound.yMax + 0.5f);

            // 中プリセットの影はオン。同じ値を押しても送る（Web の同値操作。Playwright の click と同じく見える位置まで送ってから押す）
            // Shadows are on in the medium preset; pressing the same value still sends (scrolled into view first, like a Playwright click)
            var shadowsOn = Find<VisualElement>(_view, "graphics-shadows-on");
            body.ScrollTo(shadowsOn);
            Settle();
            AssertPickable(shadowsOn);
            simulate.Click(shadowsOn);
            var record = _settingsCommands.Issued.Last();
            Assert.AreEqual(SettingsCommandKind.SetGraphicsQuality, record.Kind);
            Assert.IsTrue(record.Graphics.ShadowsEnabled);
            Assert.IsTrue(IsShown(notice));

            _settings.Set(FakeSettingsModel.Default());
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(notice));
        }

        /// <summary>
        /// Web の設定画面（本文先頭）と同じく、バーは本文に重なって幅を削らず、つまみは溝の上端にある
        /// Like the Web settings page at the top, the bar overlays the body without narrowing it and the thumb sits at the top of the track
        /// </summary>
        [Test]
        public void 設定の本文はバーに削られずつまみは本文先頭で溝の上端にある()
        {
            ShowSettings();
            var body = Find<GameScrollView>(_view, "pause-menu-body");
            var scroller = body.verticalScroller;
            var track = scroller.Q(className: "unity-scroller__slider");
            var thumb = scroller.Q(className: "unity-base-slider__dragger");
            var probe = $"body={body.worldBound} viewport={body.contentViewport.worldBound} content={body.contentContainer.worldBound} track={track.worldBound} thumb={thumb.worldBound} offset={body.scrollOffset.y} value={scroller.value} low={scroller.lowValue} high={scroller.highValue}";

            // 本文の幅は本体の幅のまま、バーは本体の右端に重なる
            // The body keeps the full width and the bar overlays its right edge
            Assert.AreEqual(body.worldBound.width, body.contentViewport.worldBound.width, 0.5f, probe);
            Assert.AreEqual(body.worldBound.width, body.contentContainer.worldBound.width, 0.5f, probe);
            Assert.AreEqual(body.worldBound.xMax, scroller.worldBound.xMax, 0.5f, probe);

            // 本文先頭ではつまみが溝の上端に付く
            // At the top of the body the thumb touches the top of the track
            Assert.AreEqual(0f, body.scrollOffset.y, 0.01f, probe);
            Assert.AreEqual(track.worldBound.y, thumb.worldBound.y, 0.5f, probe);
        }

        private void ShowSettings()
        {
            _model.Set(FakePauseMenuModel.Page(PauseMenuPage.Settings));
            Settle();
        }

        private void Settle()
        {
            // バーの出し入れで 2 回目のレイアウトが走るので 2 フレーム進める
            // Showing or hiding the bar triggers a second layout, so advance two frames
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        private void AssertPickable(VisualElement target)
        {
            var picked = panel.Pick(target.worldBound.center);
            Assert.IsTrue(picked == target || target.Contains(picked), $"picked {picked?.name}");
        }
    }
}
