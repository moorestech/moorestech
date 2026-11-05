using Client.Playtest.UiElements.GameUi;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Dsl
{
    /// <summary>
    /// GameUi のパネル矩形から Unity スクリーン矩形（左下原点）への換算を固定する（L0）
    /// Pins the conversion from a GameUi panel rect to a Unity screen rect with a bottom-left origin (L0)
    /// </summary>
    public class GameUiScreenMapperTest
    {
        private const float Tolerance = 0.01f;

        [Test]
        public void SameSizeOnlyFlipsY()
        {
            // 倍率 1 では y だけが反転する（上端 100・高さ 50 → 下端 720-150）
            // At scale 1 only y flips (top 100, height 50 becomes bottom 720-150)
            var screen = GameUiScreenMapper.Map(new Rect(100, 100, 200, 50), new Vector2(1280, 720), new Vector2(1280, 720));
            AssertNear(new Rect(100, 570, 200, 50), screen);
        }

        [Test]
        public void ScalesToFullHdScreen()
        {
            // 1280×720 のパネルを 1920×1080 に出すと 1.5 倍してから反転する
            // A 1280x720 panel shown on 1920x1080 is scaled by 1.5, then flipped
            var screen = GameUiScreenMapper.Map(new Rect(100, 100, 200, 50), new Vector2(1280, 720), new Vector2(1920, 1080));
            AssertNear(new Rect(150, 855, 300, 75), screen);
        }

        [Test]
        public void UsesEachAxisRatio()
        {
            // 縦横比の違う画面でも軸ごとの比を使う
            // Each axis uses its own ratio even when the aspect ratios differ
            var screen = GameUiScreenMapper.Map(new Rect(640, 360, 64, 36), new Vector2(1280, 720), new Vector2(2560, 1080));
            AssertNear(new Rect(1280, 486, 128, 54), screen);
        }

        private static void AssertNear(Rect expected, Rect actual)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance, "x");
            Assert.AreEqual(expected.y, actual.y, Tolerance, "y");
            Assert.AreEqual(expected.width, actual.width, Tolerance, "width");
            Assert.AreEqual(expected.height, actual.height, Tolerance, "height");
        }
    }
}
