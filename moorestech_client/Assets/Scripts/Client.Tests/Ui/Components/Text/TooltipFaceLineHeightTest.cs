using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Text;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Text
{
    /// <summary>
    /// 複数行の行送りが Web の 27.9px か
    /// Checks multi-line pitch is the Web 27.9px
    /// </summary>
    /// <remarks>
    /// Web は 18px×1.55＝27.9px。UITK の既定の行送りは約 24.5px（C2-c レビュー D5 の実測）
    /// The Web pitch is 18px x 1.55 = 27.9px; UITK's default pitch is about 24.5px (measured in C2-c review D5)
    /// </remarks>
    public class TooltipFaceLineHeightTest : UiTestHost
    {
        private const float WebLinePitch = 27.9f;

        // 320px で 3 行に折り返す文（D5 の実測で 3 行）
        // A sentence that wraps to three lines at 320px (three lines measured in D5)
        private const string ThreeLineWrappedText = "Settings Language Graphics Inventory Settings Language Graphics";

        [Test]
        public void 改行した行の行送りはWebの行の高さと1px以内()
        {
            var twoLines = LineHeight("Settings\nSettings");
            var threeLines = LineHeight("Settings\nSettings\nSettings");

            Assert.AreEqual(WebLinePitch, threeLines - twoLines, 1f);
        }

        [Test]
        public void 折り返した行の行送りは改行した行と同じ()
        {
            var wrapped = LineHeight(ThreeLineWrappedText);
            var threeLines = LineHeight("Settings\nSettings\nSettings");

            Assert.AreEqual(threeLines, wrapped, 1f);
        }

        private float LineHeight(string text)
        {
            var face = new TooltipFace("tooltip", TooltipFaceLook.Hover);
            face.style.position = Position.Absolute;
            rootVisualElement.Add(face);
            face.SetText(UiText.FromVerbatim(text));
            // 折り返しの検出と最大幅への固定で 2 回レイアウトが回る
            // Wrap detection then pinning the max width takes two layout passes
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var height = face.Q<Label>("tooltip-line").layout.height;
            face.RemoveFromHierarchy();
            return height;
        }
    }
}
