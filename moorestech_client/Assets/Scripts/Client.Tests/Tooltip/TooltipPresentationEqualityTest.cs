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
            return new TooltipPresentation(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.RequiredItems, new[] { itemName }) });
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
        public void DifferentKeyParamsOrLineCountComparesUnequal()
        {
            var baseline = RequiredItems("Iron Pickaxe");

            Assert.AreNotEqual(baseline, new TooltipPresentation(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.HoldToGet, new[] { "Iron Pickaxe" }) }));
            Assert.AreNotEqual(baseline, RequiredItems("Stone Pickaxe"));
            Assert.AreNotEqual(baseline, TooltipPresentation.Hidden);
            Assert.AreNotEqual(baseline, new TooltipPresentation(new[] { baseline.Lines[0], new TooltipLine(LocalizationKeys.Ui.Tooltip.HoldToGet) }));
        }

        // 表示状態は独立したフラグではなく行から導出されるため、行が無い＝非表示になる
        // Visibility is not an independent flag but derived from the lines, so no lines means hidden
        [Test]
        public void VisibilityIsDerivedFromLines()
        {
            Assert.IsTrue(RequiredItems("Iron Pickaxe").Visible);
            Assert.IsFalse(TooltipPresentation.Hidden.Visible);
            Assert.IsFalse(new TooltipPresentation(System.Array.Empty<TooltipLine>()).Visible);
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
