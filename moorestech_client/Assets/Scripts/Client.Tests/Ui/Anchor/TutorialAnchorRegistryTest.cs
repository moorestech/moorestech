using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Anchor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Anchor
{
    /// <summary>
    /// アンカーの印付けと照会による解決（unique・missing・duplicate）を確かめる（L1）
    /// Verifies anchor marking and query-based resolution (unique, missing, duplicate) (L1)
    /// </summary>
    public class TutorialAnchorRegistryTest : UiTestHost
    {
        [Test]
        public void MarkedElementResolvesAsUnique()
        {
            var element = new VisualElement();
            TutorialAnchor.Mark(element, AnchorIds.PauseMenu);
            rootVisualElement.Add(element);

            var result = TutorialAnchorRegistry.Resolve(rootVisualElement, "pause.menu");

            Assert.IsInstanceOf<TutorialAnchorResolution.Unique>(result);
            Assert.AreSame(element, ((TutorialAnchorResolution.Unique)result).Element);
            Assert.IsTrue(element.ClassListContains("pause.menu"));
            Assert.IsTrue(element.ClassListContains(TutorialAnchor.MarkerClassName));
        }

        [Test]
        public void RemovedElementResolvesAsMissing()
        {
            var element = new VisualElement();
            TutorialAnchor.Mark(element, AnchorIds.PauseMenu);
            rootVisualElement.Add(element);
            element.RemoveFromHierarchy();

            Assert.IsInstanceOf<TutorialAnchorResolution.Missing>(TutorialAnchorRegistry.Resolve(rootVisualElement, "pause.menu"));
        }

        [Test]
        public void SameIdOnTwoElementsResolvesAsDuplicate()
        {
            var first = new VisualElement();
            var second = new VisualElement();
            TutorialAnchor.Mark(first, AnchorIds.PauseMenu);
            TutorialAnchor.Mark(second, AnchorIds.PauseMenu);
            rootVisualElement.Add(first);
            rootVisualElement.Add(second);

            var result = TutorialAnchorRegistry.Resolve(rootVisualElement, "pause.menu");

            Assert.IsInstanceOf<TutorialAnchorResolution.Duplicate>(result);
            Assert.AreEqual(2, ((TutorialAnchorResolution.Duplicate)result).Count);
        }

        [Test]
        public void ElementWithTwoIdsResolvesUniqueByEither()
        {
            var element = new VisualElement();
            TutorialAnchor.Mark(element, AnchorIds.PauseMenu, AnchorIds.PauseSave);
            rootVisualElement.Add(element);

            Assert.IsInstanceOf<TutorialAnchorResolution.Unique>(TutorialAnchorRegistry.Resolve(rootVisualElement, "pause.menu"));
            Assert.IsInstanceOf<TutorialAnchorResolution.Unique>(TutorialAnchorRegistry.Resolve(rootVisualElement, "pause.save"));
        }

        [Test]
        public void ChildAnchorIsNotResolvedAfterParentIsRemoved()
        {
            var parent = new VisualElement();
            var child = new VisualElement();
            TutorialAnchor.Mark(child, AnchorIds.PauseBack);
            parent.Add(child);
            rootVisualElement.Add(parent);
            parent.RemoveFromHierarchy();

            Assert.IsInstanceOf<TutorialAnchorResolution.Missing>(TutorialAnchorRegistry.Resolve(rootVisualElement, "pause.back"));
        }

        [Test]
        public void 解決はログを出さず状態の遷移の報告を呼ぶ側に任せる()
        {
            var first = new VisualElement();
            var second = new VisualElement();
            TutorialAnchor.Mark(first, AnchorIds.PauseMenu);
            TutorialAnchor.Mark(second, AnchorIds.PauseMenu);
            rootVisualElement.Add(first);
            rootVisualElement.Add(second);

            // 毎フレームの追跡で呼ばれるので、0 件・複数件でも Resolve 自身は黙る
            // Called every frame by the tracker, so Resolve stays silent even for zero or multiple matches
            TutorialAnchorRegistry.Resolve(rootVisualElement, "pause.menu");
            TutorialAnchorRegistry.Resolve(rootVisualElement, "pause.save");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void クリップ容器の印は専用のクラスを付ける()
        {
            var container = new VisualElement();
            TutorialAnchor.MarkClipContainer(container);

            Assert.IsTrue(container.ClassListContains(TutorialAnchor.ClipContainerClassName));
            Assert.IsFalse(container.ClassListContains(TutorialAnchor.MarkerClassName));
        }
    }
}
