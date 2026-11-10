using Client.Ui.Foundation.Input;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Input
{
    /// <summary>
    /// ホイール素通しの印が祖先をたどって効く（L0。Web の closest）
    /// The wheel passthrough mark applies through ancestors (L0; Web closest)
    /// </summary>
    public class WheelPassthroughMarkTest
    {
        [Test]
        public void 印を付けた要素はクラスを持ち素通しになる()
        {
            var root = new VisualElement();
            WheelPassthroughMark.Mark(root);

            Assert.IsTrue(root.ClassListContains(WheelPassthroughMark.ClassName));
            Assert.IsTrue(WheelPassthroughMark.IsPassthrough(root));
        }

        [Test]
        public void 印の下の孫からも素通しになる()
        {
            var root = new VisualElement();
            var child = new VisualElement();
            var grandchild = new Label("a");
            child.Add(grandchild);
            root.Add(child);
            WheelPassthroughMark.Mark(root);

            Assert.IsTrue(WheelPassthroughMark.IsPassthrough(grandchild));
        }

        [Test]
        public void 印の無い木とnullは素通しにならない()
        {
            var root = new VisualElement();
            var child = new VisualElement();
            root.Add(child);

            Assert.IsFalse(WheelPassthroughMark.IsPassthrough(child));
            Assert.IsFalse(WheelPassthroughMark.IsPassthrough(null));
        }
    }
}
