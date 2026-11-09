using Client.Ui.Foundation.Input;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Input
{
    /// <summary>
    /// 拾わない指定が孫へ届き後付けの子へ届かない（L0）
    /// Picking-ignore reaches grandchildren, not later-added children (L0)
    /// </summary>
    public class UiPickingIgnoreTest
    {
        [Test]
        public void 根から孫まで全部拾わなくなる()
        {
            var root = new VisualElement();
            var child = new VisualElement();
            var grandchild = new Label("a");
            child.Add(grandchild);
            root.Add(child);

            UiPickingIgnore.ApplyToSubtree(root);

            Assert.AreEqual(PickingMode.Ignore, root.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, child.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, grandchild.pickingMode);
        }

        [Test]
        public void 後から足した子は掛け直すまで拾える既定のまま()
        {
            // UITK の pickingMode は継承されないので、子を作り直したら呼び直す必要がある
            // UITK pickingMode does not inherit, so it must be reapplied after rebuilding children
            var root = new VisualElement();
            UiPickingIgnore.ApplyToSubtree(root);
            var later = new VisualElement();
            root.Add(later);

            Assert.AreEqual(PickingMode.Position, later.pickingMode);
            UiPickingIgnore.ApplyToSubtree(root);
            Assert.AreEqual(PickingMode.Ignore, later.pickingMode);
        }
    }
}
