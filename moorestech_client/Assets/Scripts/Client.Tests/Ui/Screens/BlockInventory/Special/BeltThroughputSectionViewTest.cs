using Client.Input.ArrowKeys;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Special.Belt;
using Client.Ui.State.BlockInventory.Logic.Belt;
using Client.Ui.State.BlockInventory.Special.Details;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// ベルトの流量: 既定 180tick の窓・分間・直近 9 秒の棒。←/→ で窓を巡回し端で止まる（Web BeltThroughputSection の vitest）。Presenter＋View で組む
    /// Belt throughput: default 180-tick window, per-minute row and nine recent bars; ←/→ cycle the window and stop at the ends (Web vitest), built as presenter plus view
    /// </summary>
    public class BeltThroughputSectionViewTest : SpecialScreenTestHost
    {
        /// <web-test file="src/features/blockInventory/details/__tests__/BeltThroughputSection.test.ts">renders nothing without belt data</web-test>
        [Test]
        public void ベルトの詳細が無ければ何も出さない()
        {
            var (_, presenter) = Attach(SpecialBlockDetailViewModel.Empty);
            Assert.IsFalse(presenter.IsApplicable.Value);
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/BeltThroughputSection.test.ts">shows the 180-tick window and nine per-second bars by default</web-test>
        [Test]
        public void 既定は180tickの窓と9本の棒()
        {
            var (view, presenter) = Attach(SpecialDetailSamples.Only(SpecialDetailSamples.Belt));
            Assert.IsTrue(presenter.IsApplicable.Value);
            var row = (MeasuredBeltRowDisplay)BeltThroughputLogic.RowDisplay(SpecialDetailSamples.Belt.Windows[1]);
            Assert.AreEqual(Resolved(row.Text), TextOf(view, "belt-throughput-window"));
            Assert.IsTrue(IsShown(Find<Label>(view, "belt-throughput-per-minute")));
            StringAssert.Contains("60.0", TextOf(view, "belt-throughput-per-minute"));
            CollectionAssert.AreEqual(new float?[] { null, null, null, null, null, null, 1f, 0f, 1f }, BarValues(view, "belt-throughput-bars"));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/BeltThroughputSection.test.ts">switches windows with the arrow keys</web-test>
        [Test]
        public void 矢印キーで窓を切り替え端で止まる()
        {
            var (view, _) = Attach(SpecialDetailSamples.Only(SpecialDetailSamples.Belt));
            ArrowKeys.Press(HorizontalArrowKey.Right);
            Assert.AreEqual(Resolved(BeltThroughputLogic.RowDisplay(SpecialDetailSamples.Belt.Windows[2]).Text), TextOf(view, "belt-throughput-window"));
            Assert.IsFalse(IsShown(Find<Label>(view, "belt-throughput-per-minute")), "計測中は分間を出さない");
            Assert.AreEqual(9, BarValues(view, "belt-throughput-bars").Length);

            ArrowKeys.Press(HorizontalArrowKey.Left);
            ArrowKeys.Press(HorizontalArrowKey.Left);
            var first = Resolved(BeltThroughputLogic.RowDisplay(SpecialDetailSamples.Belt.Windows[0]).Text);
            Assert.AreEqual(first, TextOf(view, "belt-throughput-window"));
            StringAssert.Contains("89", first);
            ArrowKeys.Press(HorizontalArrowKey.Left);
            Assert.AreEqual(first, TextOf(view, "belt-throughput-window"));
        }

        [Test]
        public void Detachの後は切り替えずDetachで既定へ戻る()
        {
            var (view, presenter) = Attach(SpecialDetailSamples.Only(SpecialDetailSamples.Belt));
            var before = TextOf(view, "belt-throughput-window");
            ArrowKeys.Press(HorizontalArrowKey.Right);
            presenter.Detach();
            ArrowKeys.Press(HorizontalArrowKey.Right);
            presenter.Attach(OpenPanel("BeltConveyor"));
            Assert.AreEqual(before, TextOf(view, "belt-throughput-window"), "Detach の後は既定の窓へ戻る");
        }

        [Test]
        public void Detachで出し入れを偽へ戻す()
        {
            var (_, presenter) = Attach(SpecialDetailSamples.Only(SpecialDetailSamples.Belt));
            Assert.IsTrue(presenter.IsApplicable.Value);
            presenter.Detach();
            Assert.IsFalse(presenter.IsApplicable.Value, "前のパネルの出し入れを持ち越さない");
        }

        private (BeltThroughputSectionView view, BeltThroughputSectionPresenter presenter) Attach(SpecialBlockDetailViewModel detail)
        {
            var view = Mount(new BeltThroughputSectionView());
            var presenter = Presenters.Belt(view);
            Model.Set(detail);
            presenter.Attach(OpenPanel("BeltConveyor"));
            return (view, presenter);
        }
    }
}
