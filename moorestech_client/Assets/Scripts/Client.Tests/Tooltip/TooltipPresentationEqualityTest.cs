using Client.Game.InGame.UI.Tooltip;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.Tooltip
{
    /// <summary>
    ///     提示内容が同じなら配列インスタンスが違っても変化通知が出ないことを固定する
    ///     Pins that identical presentation content raises no change notification even with a fresh array instance
    /// </summary>
    public class TooltipPresentationEqualityTest
    {
        [Test]
        public void SameContentWithDifferentArrayInstancesComparesEqual()
        {
            var first = new TooltipPresentation(true, "ui.tooltip.requiredItems", new[] { "Iron Pickaxe" });
            var second = new TooltipPresentation(true, "ui.tooltip.requiredItems", new[] { "Iron Pickaxe" });

            Assert.AreEqual(first, second);
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        }

        [Test]
        public void DifferentKeyOrParamsComparesUnequal()
        {
            var baseline = new TooltipPresentation(true, "ui.tooltip.requiredItems", new[] { "Iron Pickaxe" });

            Assert.AreNotEqual(baseline, new TooltipPresentation(true, "ui.tooltip.holdToGet", new[] { "Iron Pickaxe" }));
            Assert.AreNotEqual(baseline, new TooltipPresentation(true, "ui.tooltip.requiredItems", new[] { "Stone Pickaxe" }));
            Assert.AreNotEqual(baseline, new TooltipPresentation(false, "ui.tooltip.requiredItems", new[] { "Iron Pickaxe" }));
        }

        [Test]
        public void RepeatedIdenticalPresentationPublishesOnce()
        {
            // ツール不足ブロック注視中は毎フレーム同内容が入るため、購読時の現在値1件＋実変化1件で止まる
            // Staring at a tool-gated block pushes the same content every frame, so it stops at the initial value plus one real change
            var presentation = new ReactiveProperty<TooltipPresentation>(TooltipPresentation.Hidden);
            var publishCount = 0;
            presentation.Subscribe(_ => publishCount++);

            presentation.Value = new TooltipPresentation(true, "ui.tooltip.requiredItems", new[] { "Iron Pickaxe" });
            presentation.Value = new TooltipPresentation(true, "ui.tooltip.requiredItems", new[] { "Iron Pickaxe" });

            Assert.AreEqual(2, publishCount);
        }
    }
}
