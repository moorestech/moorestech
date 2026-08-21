using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.Tooltip
{
    public class TooltipPresentationEqualityTest
    {
        private static TooltipPresentation RequiredItems(string itemName)
        {
            return new TooltipPresentation(true, new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.RequiredItems, new[] { itemName }) });
        }

        [Test]
        public void SameContentWithDifferentArrayInstancesComparesEqual()
        {
            var first = RequiredItems("Iron Pickaxe");
            var second = RequiredItems("Iron Pickaxe");

            Assert.AreEqual(first, second);
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        }

        [Test]
        public void DifferentKeyParamsLineCountOrVisibilityComparesUnequal()
        {
            var baseline = RequiredItems("Iron Pickaxe");

            Assert.AreNotEqual(baseline, new TooltipPresentation(true, new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.HoldToGet, new[] { "Iron Pickaxe" }) }));
            Assert.AreNotEqual(baseline, RequiredItems("Stone Pickaxe"));
            Assert.AreNotEqual(baseline, new TooltipPresentation(false, baseline.Lines));
            Assert.AreNotEqual(baseline, new TooltipPresentation(true, new[] { baseline.Lines[0], new TooltipLine(LocalizationKeys.Ui.Tooltip.HoldToGet) }));
        }

        [Test]
        public void RepeatedIdenticalPresentationPublishesOnce()
        {
            var presentation = new ReactiveProperty<TooltipPresentation>(TooltipPresentation.Hidden);
            var publishCount = 0;
            presentation.Subscribe(_ => publishCount++);

            presentation.Value = RequiredItems("Iron Pickaxe");
            presentation.Value = RequiredItems("Iron Pickaxe");

            Assert.AreEqual(2, publishCount);
        }
    }
}
