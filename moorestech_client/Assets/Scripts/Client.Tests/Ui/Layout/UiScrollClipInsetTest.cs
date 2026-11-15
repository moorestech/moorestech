using Client.Ui.Foundation.Layout;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Layout
{
    // Web の margin: calc(-1 * clip-inset) と padding: clip-inset の写し（4 辺と左右だけの 2 形）
    // Port of the Web margin: calc(-1 * clip-inset) plus padding: clip-inset (all four sides, or left and right only)
    public class UiScrollClipInsetTest
    {
        [Test]
        public void 四辺に負のmarginと正のpaddingが付く()
        {
            var outer = new VisualElement();
            var inner = new VisualElement();
            UiScrollClipInset.Apply(outer, inner, 6f);
            Assert.AreEqual(-6f, outer.style.marginTop.value.value);
            Assert.AreEqual(-6f, outer.style.marginLeft.value.value);
            Assert.AreEqual(6f, inner.style.paddingBottom.value.value);
            Assert.AreEqual(6f, inner.style.paddingRight.value.value);
        }

        [Test]
        public void 左右だけは上下を触らない()
        {
            var outer = new VisualElement();
            var inner = new VisualElement();
            UiScrollClipInset.ApplyHorizontal(outer, inner, 4f);
            Assert.AreEqual(-4f, outer.style.marginLeft.value.value);
            Assert.AreEqual(StyleKeyword.Null, outer.style.marginTop.keyword);
            Assert.AreEqual(4f, inner.style.paddingRight.value.value);
            Assert.AreEqual(StyleKeyword.Null, inner.style.paddingTop.keyword);
        }

        // 外と内の持ち主が違う画面（RecipeEntryList と部品の内側）は片側ずつ当て、もう片方の要素を触らない
        // Screens whose outer and inner have different owners (RecipeEntryList and the component inside) apply one side at a time without touching the other element
        [Test]
        public void 外のmarginと内のpaddingは片側ずつ当てられる()
        {
            var outer = new VisualElement();
            var inner = new VisualElement();
            UiScrollClipInset.ApplyOuterMargin(outer, 5f);
            UiScrollClipInset.ApplyInnerPadding(inner, 5f);
            Assert.AreEqual(-5f, outer.style.marginRight.value.value);
            Assert.AreEqual(StyleKeyword.Null, outer.style.paddingTop.keyword);
            Assert.AreEqual(5f, inner.style.paddingLeft.value.value);
            Assert.AreEqual(StyleKeyword.Null, inner.style.marginTop.keyword);
        }
    }
}
