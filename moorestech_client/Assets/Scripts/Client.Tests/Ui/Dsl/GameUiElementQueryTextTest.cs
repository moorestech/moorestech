using Client.Playtest.UiElements.GameUi;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Dsl
{
    /// <summary>
    /// GameUi の照会が読む文字が Web の textContent と同じ原文になるか（L1）
    /// Verifies the text read by the GameUi lookup equals the source text like the Web textContent (L1)
    /// </summary>
    public class GameUiElementQueryTextTest : UiTestHost
    {
        private const string Testid = "dsl-text-target";

        // 照会は印を除く原文だけ返す
        // A LocalizedLabel with a declared line box carries a pitch tag in text, yet the lookup returns only the source
        [Test]
        public void LocalizedLabelReadsSourceTextWithoutPitchTag()
        {
            var label = Mount(rootVisualElement, "発電中");
            label.name = Testid;

            StringAssert.StartsWith("<line-height=", label.text);
            Assert.AreEqual("発電中", Find().Text);
        }

        // 連結も印を除く原文を返す
        // A box concatenating child LocalizedLabels also returns the concatenated sources without tags
        [Test]
        public void ContainerConcatenatesLocalizedLabelSources()
        {
            var box = new VisualElement { name = Testid };
            box.style.position = Position.Absolute;
            box.style.width = 400;
            rootVisualElement.Add(box);
            Mount(box, "供給電力 0");
            Mount(box, " / 最大 100");

            Assert.AreEqual("供給電力 0 / 最大 100", Find().Text);
        }

        private Client.Playtest.UiElements.UiElementSnapshot Find()
        {
            return GameUiElementQuery.Find(rootVisualElement, Testid, new FlipOnlyScreenMapper());
        }

        // 本番同様に置き、行の印までレイアウトを回す
        // Mount the production LocalizedLabel with a Typography class and run layout until the pitch tag is attached
        private LocalizedLabel Mount(VisualElement parent, string words)
        {
            var label = new LocalizedLabel();
            label.AddToClassList(UiTypographyClass.TextSm);
            label.SetText(UiText.FromVerbatim(words));
            parent.Add(label);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return label;
        }

        // 倍率 1 の画面を仮定し、y の反転だけを行う
        // Assumes a screen at scale 1 and only flips y
        private sealed class FlipOnlyScreenMapper : IGameUiScreenMapper
        {
            public Rect PanelToScreen(Rect panelRect)
            {
                return new Rect(panelRect.x, 720 - panelRect.yMax, panelRect.width, panelRect.height);
            }
        }
    }
}
