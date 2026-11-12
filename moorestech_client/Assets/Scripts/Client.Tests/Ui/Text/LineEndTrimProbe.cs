using System.Text;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text
{
    // 使い捨てのプローブ 2（次のコミットで消す）
    // Throwaway probe 2 (removed in the next commit)
    public class LineEndTrimProbe : UiTestHost
    {
        private const string Tag = "<line-height=18px>";
        private const string Variant = "研究を完了できません（前提条件または素材が不足しています<space=-0.5em>）<space=0.5em>";
        private const string Clean2 = "研究を完了できません（前提条\n件または素材が不足しています）";

        [Test]
        public void Probe()
        {
            foreach (var selectable in new[] { false, true })
            {
                var label = Mount(Tag + Variant, 230f, WhiteSpace.Normal);
                label.selection.isSelectable = selectable;
                simulate.FrameUpdate();
                var sb = new StringBuilder();
                for (var i = 0; i <= label.text.Length; i++)
                {
                    label.selection.cursorIndex = i;
                    var p = label.selection.cursorPosition;
                    sb.Append($"{i}:{p.x:F1},{p.y:F1} ");
                }
                Log($"cursor selectable={selectable} len={label.text.Length} h={label.layout.height:F1} :: {sb}");
                label.parent.RemoveFromHierarchy();
            }

            var nowrap = Mount(Tag + Clean2, 230f, WhiteSpace.NoWrap);
            Log($"nowrap+newline fixed230 h={nowrap.layout.height:F1} w={nowrap.layout.width:F1}");
            nowrap.parent.RemoveFromHierarchy();

            var nowrapAuto = Mount(Tag + Clean2, -1f, WhiteSpace.NoWrap);
            Log($"nowrap+newline auto h={nowrapAuto.layout.height:F1} w={nowrapAuto.layout.width:F1}");
            nowrapAuto.parent.RemoveFromHierarchy();

            var normal = Mount(Tag + Clean2, 230f, WhiteSpace.Normal);
            Log($"normal+newline h={normal.layout.height:F1}");
            normal.parent.RemoveFromHierarchy();

            var nobr = Mount(Tag + "研究を完了できません（前提条\n<nobr>件または素材が不足しています）</nobr>", 230f, WhiteSpace.Normal);
            Log($"normal+newline+nobr h={nobr.layout.height:F1}");
            nobr.parent.RemoveFromHierarchy();

            var rendered = Mount("source", 230f, WhiteSpace.NoWrap);
            rendered.experimental.SetRenderedText(Tag + Clean2);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Log($"setRenderedText nowrap h={rendered.layout.height:F1} text='{rendered.text}'");
            rendered.parent.RemoveFromHierarchy();

            Assert.Pass();
        }

        private static void Log(string message)
        {
            Debug.Log($"[LineEndTrimProbe2] {message}");
        }

        private Label Mount(string text, float width, WhiteSpace whiteSpace)
        {
            var locale = new VisualElement();
            locale.AddToClassList(GameUiLocaleClass.CjkClassName);
            rootVisualElement.Add(locale);
            var label = new Label(text) { enableRichText = true };
            label.style.position = Position.Absolute;
            label.style.fontSize = 15f;
            label.style.letterSpacing = 5.5f;
            label.style.whiteSpace = whiteSpace;
            label.style.marginLeft = 0; label.style.marginRight = 0; label.style.paddingLeft = 0; label.style.paddingRight = 0;
            if (width > 0) label.style.width = width;
            locale.Add(label);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return label;
        }
    }
}
