using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Theme;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ThemeTokenFile;

namespace Client.Tests.Ui.Components.Controls.RecipeAction
{
    /// <summary>
    /// ホバーで面が描き直される（L1）
    /// Hovering repaints the face (L1)
    /// </summary>
    public class RecipeActionButtonHoverRepaintTest : UiTestHost
    {
        [Test]
        public void ホバーで面の3色がホバーの色へ解決し直され帯の頂点に出る()
        {
            Localize.Initialize();
            var button = new RecipeActionButton("recipe-action-hover-test", new UiText(LocalizationKeys.Ui.Recipe.CraftButtonLabel), RecipeActionButtonVariant.Craft);
            rootVisualElement.Add(button);
            simulate.FrameUpdate();
            var face = button.Q<RecipeActionButtonFace>(className: RecipeActionButtonFace.UssClassName);
            var drawCount = 0;
            face.generateVisualContent += _ => drawCount++;
            simulate.FrameUpdate();
            var normal = BuildFaceStrip(face);
            var drawCountBeforeHover = drawCount;
            AssertTint(ResolveColor("--recipe-action-background-start"), normal[0], "normal start");

            // 本番と同じポインタの移動で :hover を付け、面を組む入力（解決済みの 3 色）から帯を組み直して頂点を読む
            // Apply :hover through the same pointer move as production, then rebuild the strip from the face's resolved inputs and read its vertices
            simulate.MouseMove(button.worldBound.center);
            simulate.FrameUpdate();
            // 本番の描き直し経路の通過を数える
            // Count passes through the production repaint path
            Assert.Greater(drawCount, drawCountBeforeHover, "hover repaints the face");
            var hover = BuildFaceStrip(face);
            AssertTint(ResolveColor("--recipe-action-hover-start"), hover[0], "hover start");
            AssertTint(ResolveColor("--recipe-action-hover-middle"), hover[RecipeActionFaceMesh.SegmentsPerHalf], "hover middle");
            AssertTint(ResolveColor("--recipe-action-hover-end"), hover[RecipeActionFaceMesh.ColumnCount - 1], "hover end");
            Assert.AreNotEqual(normal[0], hover[0], "hover differs from normal");
        }

        private static Vertex[] BuildFaceStrip(RecipeActionButtonFace face)
        {
            Assert.IsTrue(UssCustomStyleReader.TryReadColor(face.customStyle, RecipeActionButton.FaceStartProperty, out var start));
            Assert.IsTrue(UssCustomStyleReader.TryReadColor(face.customStyle, RecipeActionButton.FaceMiddleProperty, out var middle));
            Assert.IsTrue(UssCustomStyleReader.TryReadColor(face.customStyle, RecipeActionButton.FaceEndProperty, out var end));
            Assert.IsTrue(UssCustomStyleReader.TryReadPixels(face.customStyle, RecipeActionButton.FaceBorderWidthProperty, out var borderWidth));
            var vertices = new Vertex[RecipeActionFaceMesh.VertexCount];
            var indices = new ushort[RecipeActionFaceMesh.IndexCount];
            var size = face.contentRect.size;
            RecipeActionFaceMesh.Build(size.x, size.y, borderWidth, start, middle, end, vertices, indices);
            return vertices;
        }

        private static void AssertTint(Color expected, Vertex actual, string label)
        {
            Assert.AreEqual((Color32)expected, actual.tint, label);
        }
    }
}
