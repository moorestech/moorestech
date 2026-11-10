using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Anchor;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Anchor
{
    /// <summary>
    /// 印を外す口が id を外し、id が残らないときだけ印のクラスを外すことを確かめる（L1）
    /// Verifies Unmark removes ids and drops the marker class only when no id remains (L1)
    /// </summary>
    public class TutorialAnchorUnmarkTest : UiTestHost
    {
        [Test]
        public void UnmarkingLastIdRemovesMarkerAndResolvesMissing()
        {
            var element = new VisualElement();
            element.AddToClassList("item-slot");
            var id = AnchorIds.InventoryItem("00000000-0000-0000-0000-000000000001");
            TutorialAnchor.Mark(element, id);
            rootVisualElement.Add(element);

            TutorialAnchor.Unmark(element, id);

            Assert.IsFalse(element.ClassListContains(id));
            Assert.IsFalse(element.ClassListContains(TutorialAnchor.MarkerClassName));
            Assert.IsTrue(element.ClassListContains("item-slot"));
            Assert.IsInstanceOf<TutorialAnchorResolution.Missing>(TutorialAnchorRegistry.Resolve(rootVisualElement, id));
        }

        [Test]
        public void UnmarkingOneOfTwoIdsKeepsMarkerForTheOther()
        {
            var element = new VisualElement();
            TutorialAnchor.Mark(element, AnchorIds.PauseMenu, AnchorIds.EquipmentSlot(2));
            rootVisualElement.Add(element);

            TutorialAnchor.Unmark(element, AnchorIds.PauseMenu);

            Assert.IsFalse(element.ClassListContains(AnchorIds.PauseMenu));
            Assert.IsTrue(element.ClassListContains(TutorialAnchor.MarkerClassName));
            Assert.IsInstanceOf<TutorialAnchorResolution.Unique>(TutorialAnchorRegistry.Resolve(rootVisualElement, AnchorIds.EquipmentSlot(2)));
        }
    }
}
