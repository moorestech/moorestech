using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Special.Energy;
using Client.Ui.State.BlockInventory.Logic.Detail;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 歯車の区画: 消費側は基準 RPM 付き、発電側は現在値だけ。トルク 0 でも同じ 2 行（Web GearSection の vitest）
    /// Gear section: consumers show RPM with its base, generators only the current; zero torque keeps the same two rows (Web GearSection vitest)
    /// </summary>
    public class GearSectionViewTest : SpecialScreenTestHost
    {
        /// <web-test file="src/features/blockInventory/details/__tests__/GearSection.test.ts">renders consumed torque without a denominator and RPM with its base for consumers</web-test>
        [Test]
        public void 消費側は分母無しのトルクと基準付きのRPMを出す()
        {
            var gear = GearDetail.Consumer(5, 2.4, 10);
            var view = Render(gear);
            Assert.AreEqual(Resolved(GearRowDisplay.From(gear).Torque), TextOf(view, "gear-torque"));
            Assert.AreEqual(Resolved(GearRowDisplay.From(gear).Rpm), TextOf(view, "gear-rpm"));
            StringAssert.Contains("2.4", TextOf(view, "gear-torque"));
            StringAssert.Contains("10.0", TextOf(view, "gear-rpm"));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearSection.test.ts">renders generated torque and current RPM only for generators</web-test>
        [Test]
        public void 発電側は発生トルクと現在のRPMだけを出す()
        {
            var gear = GearDetail.Generator(20, 5);
            var view = Render(gear);
            Assert.AreEqual(Resolved(GearRowDisplay.From(gear).Torque), TextOf(view, "gear-torque"));
            Assert.AreEqual(Resolved(GearRowDisplay.From(gear).Rpm), TextOf(view, "gear-rpm"));
            StringAssert.DoesNotContain("/", TextOf(view, "gear-rpm"));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearSection.test.ts">keeps the same rows for a zero-torque transmission block</web-test>
        [Test]
        public void トルク0の伝達ブロックも同じ2行()
        {
            var view = Render(GearDetail.Consumer(5, 0, 5));
            CollectionAssert.AreEqual(new[] { "gear-section", "gear-torque", "gear-rpm" }, view.Query<VisualElement>().Where(e => !string.IsNullOrEmpty(e.name)).ToList().ConvertAll(e => e.name));
        }

        private GearSectionView Render(GearDetail gear)
        {
            var view = Mount(new GearSectionView());
            view.Render(NoSlots, gear);
            return view;
        }
    }
}
