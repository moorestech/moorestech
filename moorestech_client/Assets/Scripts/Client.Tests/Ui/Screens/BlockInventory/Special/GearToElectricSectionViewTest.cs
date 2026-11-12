using Client.Localization;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.Screens.BlockInventory.Special.Energy;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 回転発電機の区画: 発電状態（不足トーン）と計測時期の説明、供給電力 current / max（Web GearToElectricSection の vitest）
    /// GearToElectric section: generation state with the shortage tone and timing note, supplied power current / max (Web GearToElectricSection vitest)
    /// </summary>
    public class GearToElectricSectionViewTest : SpecialScreenTestHost
    {
        /// <web-test file="src/features/blockInventory/details/__tests__/GearToElectricSection.test.ts">shows supplied power over the master maximum while generating</web-test>
        [Test]
        public void 発電中は最大に対する供給電力を不足トーン無しで出す()
        {
            var view = Render(GearToElectricDisplayState.Generating, 72.4, 100);
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BlockInventory.GearToElectricStateGenerating), TextOf(view, "gear-to-electric-state"));
            Assert.AreEqual(Supplied("72", "100"), TextOf(view, "gear-to-electric-supplied-power"));
            Assert.IsFalse(IsInsufficient(view));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearToElectricSection.test.ts">shows idle without an insufficient tone</web-test>
        [Test]
        public void 待機は不足トーン無し()
        {
            var view = Render(GearToElectricDisplayState.Idle, 0, 100);
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BlockInventory.GearToElectricStateIdle), TextOf(view, "gear-to-electric-state"));
            Assert.IsFalse(IsInsufficient(view));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearToElectricSection.test.ts">shows stopped with an insufficient tone</web-test>
        [Test]
        public void 停止は不足トーンで出す()
        {
            var view = Render(GearToElectricDisplayState.Stopped, 0, 100);
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BlockInventory.GearToElectricStateStopped), TextOf(view, "gear-to-electric-state"));
            Assert.IsTrue(IsInsufficient(view));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearToElectricSection.test.ts">preserves positive supply %s as %s</web-test>
        [TestCase(0d, "0")]
        [TestCase(0.1d, "0.10")]
        [TestCase(0.49d, "0.49")]
        [TestCase(0.5d, "1")]
        [TestCase(29.49999d, "29")]
        [TestCase(29.5d, "30")]
        [TestCase(1e-8d, "1.0e-8")]
        public void 正の供給は0に潰さず桁を残す(double supplied, string expected)
        {
            Assert.AreEqual(Supplied(expected, "100"), TextOf(Render(GearToElectricDisplayState.Generating, supplied, 100), "gear-to-electric-supplied-power"));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearToElectricSection.test.ts">preserves a positive master maximum below integer precision</web-test>
        [Test]
        public void 整数に満たない最大も桁を残す()
        {
            Assert.AreEqual(Supplied("0.10", "0.20"), TextOf(Render(GearToElectricDisplayState.Generating, 0.1, 0.2), "gear-to-electric-supplied-power"));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearToElectricSection.test.ts">explains the measurement timing on the state row</web-test>
        [Test]
        public void 状態の行にホバーで計測時期の説明を持つ()
        {
            var view = Render(GearToElectricDisplayState.Generating, 10, 100);
            Assert.IsTrue(HoverTooltipMark.TryFindMarked(Find<VisualElement>(view, "gear-to-electric-state"), out _, out var text));
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BlockInventory.GearToElectricMeasurementTiming), text.Resolve());
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearToElectricSection.test.ts">renders nothing before detail arrives</web-test>
        [Test]
        public void 詳細の前は何も出さない()
        {
            // 出し入れは Presenter の IsApplicable が親へ知らせる（区画は自分の display を触らない。D-23）
            // Visibility is reported by the presenter's IsApplicable (the section never touches its own display. D-23)
            var presenter = Presenters.Of(Mount(new GearToElectricSectionView()), SpecialDetailSection.GearToElectric);
            presenter.Attach(OpenPanel("GearToElectricGenerator"));
            Assert.IsFalse(presenter.IsApplicable.Value);
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/GearToElectricSection.i18n.test.ts">shows the specified supplied-power sentence after real i18n interpolation</web-test>
        [Test]
        public void 日本語の実際の文で供給電力と状態と説明を出す()
        {
            Localize.TrySetChosenLanguage("japanese");
            var view = Render(GearToElectricDisplayState.Generating, 72.4, 100);
            Assert.AreEqual("供給電力 72 / 最大 100", TextOf(view, "gear-to-electric-supplied-power"));
            Assert.AreEqual("発電中", TextOf(view, "gear-to-electric-state"));
            HoverTooltipMark.TryFindMarked(Find<VisualElement>(view, "gear-to-electric-state"), out _, out var tooltip);
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BlockInventory.GearToElectricMeasurementTiming), tooltip.Resolve());
        }

        private GearToElectricSectionView Render(GearToElectricDisplayState state, double supplied, double max)
        {
            var view = Mount(new GearToElectricSectionView());
            view.Render(NoSlots, SpecialDetailSamples.Only(new GearToElectricDetail(state, supplied, max)));
            return view;
        }

        private static string Supplied(string current, string max)
        {
            return new UiText(LocalizationKeys.Ui.BlockInventory.GearToElectricSuppliedPower, new[] { new LocalizationTextParam("current", current), new LocalizationTextParam("max", max) }).Resolve();
        }

        private static bool IsInsufficient(VisualElement view)
        {
            return Find<VisualElement>(view, "gear-to-electric-state").ClassListContains(LackHighlightText.InsufficientClassName);
        }
    }
}
