using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text
{
    // 使い捨てのプローブ（次のコミットで消す）
    // Throwaway probe (removed in the next commit)
    public class LineEndTrimProbe : UiTestHost
    {
        private const string Head = "研究を完了できません（前提条件または素材が不足しています";

        [Test]
        public void Probe()
        {
            var variants = new[]
            {
                ("plain", "）"),
                ("a-space-after", "）<space=-0.5em>"),
                ("b-cspace", "<cspace=-0.5em>）</cspace>"),
                ("mspace", "<mspace=0.5em>）</mspace>"),
                ("margin-em", "<margin-right=-0.5em>）</margin-right>"),
                ("margin-px", "<margin-right=-7.5px>）</margin-right>"),
                ("space-before", "<space=-0.5em>）"),
                ("space-before-after", "<space=-0.5em>）<space=0.5em>"),
                ("newline", "）"),
            };
            foreach (var generator in new[] { TextGeneratorType.Standard, TextGeneratorType.Advanced })
            {
                var oneLine = Measure("研究", 230f, generator).y;
                foreach (var (name, paren) in variants)
                {
                    var sentence = name == "newline" ? "研究を完了できません（前提条\n件または素材が不足しています）" : Head + paren;
                    var at230 = Measure(sentence, 230f, generator);
                    var at224 = Measure(sentence, 224f, generator);
                    var midPlain = Measure("前提）条件", 1000f, generator, true).x;
                    var mid = Measure("前提" + paren + "条件", 1000f, generator, true).x;
                    var tailPlain = Measure("前提）", 1000f, generator, true).x;
                    var tail = Measure("前提" + paren, 1000f, generator, true).x;
                    var mts = MeasureApi(sentence, 230f, generator);
                    Debug.Log($"[LineEndTrimProbe] gen={generator} v={name} oneLineH={oneLine:F2} h230={at230.y:F2} h224={at224.y:F2} midW={mid:F2} (plain {midPlain:F2}) tailW={tail:F2} (plain {tailPlain:F2}) mts230={mts.x:F2}x{mts.y:F2}");
                }
            }
            Assert.Pass();
        }

        private Vector2 Measure(string text, float width, TextGeneratorType generator, bool noWrap = false)
        {
            var label = Mount(text, generator);
            if (noWrap) label.style.whiteSpace = WhiteSpace.NoWrap;
            else label.style.width = width;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var size = new Vector2(label.layout.width, label.layout.height);
            label.parent.RemoveFromHierarchy();
            return size;
        }

        private Vector2 MeasureApi(string text, float width, TextGeneratorType generator)
        {
            var label = Mount(text, generator);
            label.style.width = width;
            simulate.FrameUpdate();
            var size = label.MeasureTextSize(text, width, VisualElement.MeasureMode.AtMost, 0, VisualElement.MeasureMode.Undefined);
            label.parent.RemoveFromHierarchy();
            return size;
        }

        private Label Mount(string text, TextGeneratorType generator)
        {
            var locale = new VisualElement();
            locale.AddToClassList(GameUiLocaleClass.CjkClassName);
            rootVisualElement.Add(locale);
            var label = new Label(text) { enableRichText = true };
            label.style.position = Position.Absolute;
            label.style.fontSize = 15f;
            label.style.letterSpacing = 5.5f;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginLeft = 0; label.style.marginRight = 0; label.style.paddingLeft = 0; label.style.paddingRight = 0;
            label.style.unityTextGenerator = generator;
            locale.Add(label);
            return label;
        }
    }
}
