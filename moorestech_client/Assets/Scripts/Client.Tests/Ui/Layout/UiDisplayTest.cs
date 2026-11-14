using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Layout;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Layout
{
    /// <summary>
    /// 出し入れの唯一の口（隠すとインラインの none、出すとインラインを外す）
    /// The single show/hide entry (hide sets inline none, show removes the inline value)
    /// </summary>
    public class UiDisplayTest : UiTestHost
    {
        private const string StateClassProbeSheetPath = "Assets/Scripts/Client.Tests/Ui/Layout/UiDisplayStateClassProbe.uss";

        [Test]
        public void 隠すとインラインのnoneが付く()
        {
            var element = new VisualElement();
            UiDisplay.SetShown(element, false);
            Assert.AreEqual(DisplayStyle.None, element.style.display.value);
            Assert.AreEqual(StyleKeyword.Undefined, element.style.display.keyword);
            Assert.IsFalse(UiDisplay.IsShownInline(element));
        }

        [Test]
        public void 出すとインラインが外れUSSの値に戻る()
        {
            var element = new VisualElement();
            UiDisplay.SetShown(element, false);
            UiDisplay.SetShown(element, true);
            Assert.AreEqual(StyleKeyword.Null, element.style.display.keyword);
            Assert.IsTrue(UiDisplay.IsShownInline(element));
        }

        // 部品の USS が状態クラスで隠している要素は、画面が出しても隠れたまま（Flex を当てると状態クラスに勝ってしまうのが DC-11 の症状）
        // An element hidden by its component USS state class stays hidden when the screen shows it (inline Flex beating the class was the DC-11 symptom)
        [Test]
        public void 状態クラスのnoneは出しても負けない()
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StateClassProbeSheetPath);
            if (sheet == null) Assert.Fail($"StyleSheet not found: {StateClassProbeSheetPath}");
            rootVisualElement.styleSheets.Add(sheet);
            var element = new VisualElement();
            element.AddToClassList("ui-display-probe--hidden");
            rootVisualElement.Add(element);
            UiDisplay.SetShown(element, false);
            UiDisplay.SetShown(element, true);
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, element.resolvedStyle.display);

            // 状態クラスを外すと USS の既定に戻って出る
            // Removing the state class falls back to the USS default and shows it
            element.RemoveFromClassList("ui-display-probe--hidden");
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.Flex, element.resolvedStyle.display);
        }
    }
}
