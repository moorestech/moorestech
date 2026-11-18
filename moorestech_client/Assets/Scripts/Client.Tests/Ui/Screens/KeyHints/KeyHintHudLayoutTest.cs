using System.Globalization;
using Client.Game.InGame.UI.UIState.State;
using Client.Tests.Ui.Components;
using Client.Ui.Screens.KeyHints;
using Client.Ui.State.KeyHints;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.KeyHints
{
    /// <summary>
    /// キーヒントの左下の配置・削除モードの持ち上げ・行の高さを 720p と 2560×1080 相当で確かめる（L1）
    /// Verifies the bottom-left placement, delete-mode lift and row height of the key hints at 720p and the 2560x1080 equivalent (L1)
    /// </summary>
    [TestFixture(1280f, 720f, 1f)]
    [TestFixture(1706.6666f, 720f, 1.5f)]
    public class KeyHintHudLayoutTest : ScreenTestHost
    {
        private static readonly KeyHint TabInventory = new(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Inventory);
        private static readonly KeyHint GExit = new(LocalizationKeys.Ui.KeyHint.Key.G, LocalizationKeys.Ui.KeyHint.Text.ExitDeleteMode);
        private readonly Vector2 _logicalPanelSize;
        private readonly float _pixelsPerPoint;

        public KeyHintHudLayoutTest(float width, float height, float panelPixelsPerPoint)
        {
            _logicalPanelSize = new Vector2(width, height);
            _pixelsPerPoint = panelPixelsPerPoint;
        }

        [OneTimeSetUp]
        public void SetUpLogicalPanelSize()
        {
            panelSize = _logicalPanelSize;
        }

        [SetUp]
        public void SetUpPanelPixelsPerPoint()
        {
            pixelsPerPoint = _pixelsPerPoint;
        }

        [Test]
        public void 左下に積み削除モードでは警告帯の高さだけ上がる()
        {
            var view = Mount(new KeyHintHudView());
            view.Render(KeyHintHudViewModel.Create(new[] { TabInventory, GExit }, false, false));
            simulate.FrameUpdate();
            var gap = ThemeTokenFile.ResolvePixels("--tutorial-key-hint-kbd-gap");

            // 通常は left 7px・bottom 8px、行の間は Mantine の xs（10px）、kbd と文言の間は --tutorial-key-hint-kbd-gap
            // Normally left 7px and bottom 8px, rows 10px apart (Mantine xs), kbd-to-text gap --tutorial-key-hint-kbd-gap
            // left 7px は ppp 1.5 で 10.5 物理画素になり、UITK は物理画素へ切り上げで寄せる（GamePanelCraftTest と同じく期待値を寄せる）
            // left 7px is 10.5 device pixels at ppp 1.5 and UITK snaps it half-up to device pixels (snap the expectation as GamePanelCraftTest does)
            Assert.AreEqual(Mathf.Floor(7f * _pixelsPerPoint + 0.5f) / _pixelsPerPoint, view.worldBound.xMin, 0.05f);
            Assert.AreEqual(rootVisualElement.worldBound.yMax - 8f, view.worldBound.yMax, 0.05f);
            var rows = view.Query(className: "key-hint-row").ToList();
            Assert.AreEqual(10f, rows[1].worldBound.yMin - rows[0].worldBound.yMax, 0.05f);
            Assert.AreEqual(gap, rows[0][1].worldBound.xMin - rows[0][0].worldBound.xMax, 0.05f);

            // 削除モードは帯の高さ＋8px
            // Delete mode lifts it by the band height plus 8px
            view.Render(KeyHintHudViewModel.Create(new[] { TabInventory, GExit }, true, false));
            simulate.FrameUpdate();
            var band = ThemeTokenFile.ResolvePixels("--delete-mode-warning-band-height");
            Assert.AreEqual(rootVisualElement.worldBound.yMax - band - 8f, view.worldBound.yMax, 0.05f);
            foreach (var element in view.Query<VisualElement>().ToList()) Assert.AreEqual(PickingMode.Ignore, element.pickingMode, element.name);
        }

        [Test]
        public void 行の高さはWebの字の大きさと行高の積になる()
        {
            var view = Mount(new KeyHintHudView());
            view.Render(KeyHintHudViewModel.Create(new[] { TabInventory, GExit }, false, false));
            simulate.FrameUpdate();

            // Web の line-height は字の大きさ × 行高（25 × 1.2 = 30px）。値はトークンから引く
            // The Web line-height is font size times line height (25 x 1.2 = 30px); values come from tokens
            var expected = ThemeTokenFile.ResolvePixels("--key-hint-font-size") * float.Parse(ThemeTokenFile.Resolve("--key-hint-line-height"), CultureInfo.InvariantCulture);
            foreach (var row in view.Query(className: "key-hint-row").ToList()) Assert.AreEqual(expected, row.worldBound.height, 0.05f);
        }
    }
}
