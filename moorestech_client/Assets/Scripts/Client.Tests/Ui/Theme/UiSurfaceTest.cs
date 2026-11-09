using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Theme;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Theme
{
    /// <summary>
    /// 面を張る部品の中の要素は面の上と分かり、面の外と面そのものは面の上と数えない（L0）
    /// Elements inside surface components count as on a surface; elements outside and the surface itself do not (L0)
    /// </summary>
    public class UiSurfaceTest
    {
        [Test]
        public void 面を張る部品の中の要素は面の上と分かる()
        {
            // グローの色を地で選ぶ読み手（チュートリアル枠）が頼る 4 部品
            // The four components the per-ground glow reader (tutorial outline) relies on
            var surfaces = new VisualElement[]
            {
                new GamePanel("test-game-panel", GamePanelFrame.Default),
                new GamePanelSurface("test-game-panel-surface", GamePanelSurfaceKind.Hud),
                new DialogFrame("test-dialog-frame"),
                new PaperSurface("test-paper-surface"),
            };
            foreach (var surface in surfaces)
            {
                var child = new VisualElement();
                surface.Add(child);
                Assert.IsTrue(UiSurface.HasSurfaceAncestor(child), surface.GetType().Name);
                Assert.IsFalse(UiSurface.HasSurfaceAncestor(surface), surface.GetType().Name);
            }
        }

        [Test]
        public void 面の外の要素は面の上と数えない()
        {
            var root = new VisualElement();
            var child = new VisualElement();
            root.Add(child);
            Assert.IsFalse(UiSurface.HasSurfaceAncestor(child));
        }
    }
}
