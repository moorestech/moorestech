using System;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Special.Space;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Text;
using Game.Block.Interface.State;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Space
{
    /// <summary>
    /// 宇宙エレベーターの通しの振る舞い（接続・状態の描画・入口と出口の絶対添字・取得待ち・並べ替え。Web spaceElevator.spec.ts）
    /// Space elevator end-to-end behaviour (connect, state rendering, absolute inlet/outlet indices, loading, reordering. Web spaceElevator.spec.ts)
    /// </summary>
    public class SpaceElevatorSpecTest : SpaceElevatorTestHost
    {
        /// <web-test file="e2e/tests/block/spaceElevator/spaceElevator.spec.ts">接続ボタンでactionを送り、接続後は進みを出す</web-test>
        [Test]
        public void 接続ボタンで送り接続後は進みを出す()
        {
            var view = Render(Slots(false), Unconnected(SpaceElevatorBaseStatus.ReadyToConnect));
            Assert.IsTrue(IsShown(Find<VisualElement>(view, "space-elevator-section")));
            StringAssert.Contains(new UiText(LocalizationKeys.Ui.BlockInventory.SpaceElevatorSatelliteInput).Resolve(), AllText(Find<VisualElement>(view, "space-elevator-grid")));
            StringAssert.Contains(new UiText(LocalizationKeys.Ui.BlockInventory.SpaceElevatorResultOutput).Resolve(), AllText(Find<VisualElement>(view, "space-elevator-grid")));
            StringAssert.Contains("4000", TextOf(view, "electric-network-summary"));
            StringAssert.Contains("2000", TextOf(view, "electric-network-summary"));
            StringAssert.Contains("1000", TextOf(view, "space-elevator-pool"));

            simulate.Click(Find<Button>(view, "space-elevator-connect"));
            CollectionAssert.AreEqual(new[] { "space_elevator.connect" }, Commands.Issued);
            view.Render(Slots(false), SpecialDetailSamples.ElevatorWithNetwork(SpecialDetailSamples.SpaceElevator(SpaceElevatorBaseStatus.Climbing, true, 1000, 0f)));
            Assert.IsTrue(IsShown(Find<VisualElement>(view, "space-elevator-progress")));
            Assert.IsFalse(IsShown(Find<Button>(view, "space-elevator-connect")));
        }

        /// <web-test file="e2e/tests/block/spaceElevator/spaceElevator.spec.ts">${fixture} の状態と進捗を描画する</web-test>
        [TestCase(SpaceElevatorBaseStatus.InsufficientCable, false, -1f)]
        [TestCase(SpaceElevatorBaseStatus.Climbing, true, 0.5f)]
        [TestCase(SpaceElevatorBaseStatus.OutputBlocked, true, 1f)]
        public void 不足と昇降と出口待ちの状態と進捗を描く(SpaceElevatorBaseStatus status, bool connected, float progress)
        {
            var view = Render(Slots(false), SpecialDetailSamples.SpaceElevator(status, connected, 1000, connected ? progress : 0f));
            Assert.IsTrue(IsShown(Find<VisualElement>(view, "space-elevator-view")));
            StringAssert.Contains("100%", TextOf(view, "electric-network-summary"));
            Assert.AreEqual(new UiText(SpaceElevatorLogic.StatusDisplay(status).LabelKey).Resolve(), TextOf(view, "space-elevator-status"));
            Assert.AreEqual(connected, IsShown(Find<VisualElement>(view, "space-elevator-progress")));
            Assert.AreEqual(!connected, IsShown(Find<Button>(view, "space-elevator-connect")));
            if (connected) Assert.AreEqual(progress, GaugeValue(view, "space-elevator-progress"), 0.001f);
            else Assert.IsFalse(Find<Button>(view, "space-elevator-connect").enabledSelf);
        }

        /// <web-test file="e2e/tests/block/spaceElevator/spaceElevator.spec.ts">入口への投入は元slot0を送り出口を変えない</web-test>
        [Test]
        public void 入口の押下は元の添字0で届く()
        {
            var view = Render(Slots(false), Unconnected(SpaceElevatorBaseStatus.ReadyToConnect));
            simulate.Click(Find<VisualElement>(view, "space-elevator-input-grid-slot-0"));
            CollectionAssert.AreEqual(new[] { "LeftDown 0" }, BlockSlots.Issued);
        }

        /// <web-test file="e2e/tests/block/spaceElevator/spaceElevator.spec.ts">出口${outputIndex + 1}の回収は元slot${outputIndex + 1}を送る</web-test>
        [TestCase(0)]
        [TestCase(1)]
        public void 出口の押下は元の添字で届く(int outputIndex)
        {
            var view = Render(Slots(false), Unconnected(SpaceElevatorBaseStatus.ReadyToConnect));
            simulate.Click(Find<VisualElement>(view, $"space-elevator-output-grid-slot-{outputIndex + 1}"));
            CollectionAssert.AreEqual(new[] { $"LeftDown {outputIndex + 1}" }, BlockSlots.Issued);
        }

        /// <web-test file="e2e/tests/block/spaceElevator/spaceElevator.spec.ts">詳細の取得待ちから接続可能へ遷移する</web-test>
        [Test]
        public void 取得待ちから接続可能へ移る()
        {
            var view = Mount(new SpaceElevatorBodyView(Kit(LoadContent())));
            view.Render(Slots(false), SpecialBlockDetailViewModel.Empty);
            Assert.IsTrue(IsShown(Find<Label>(view, "space-elevator-loading")));
            view.Render(Slots(false), SpecialDetailSamples.ElevatorWithNetwork(Unconnected(SpaceElevatorBaseStatus.ReadyToConnect)));
            Assert.IsFalse(IsShown(Find<Label>(view, "space-elevator-loading")));
            Assert.IsTrue(Find<Button>(view, "space-elevator-connect").enabledSelf);
        }

        /// <web-test file="e2e/tests/block/spaceElevator/spaceElevator.spec.ts">ホストが並べ替えた入口と出口の添字を描画と操作に使う</web-test>
        [Test]
        public void 並べ替えた入口と出口の添字を描画と押下に使う()
        {
            var reordered = new SpaceElevatorDetail(new[] { 2 }, new[] { 1, 0 }, Array.Empty<InletSlotBinding>(), false, 1000, 1000, 0f, SpaceElevatorBaseStatus.ReadyToConnect);
            var view = Render(Slots(false), SpecialDetailSamples.ElevatorWithNetwork(reordered));
            Assert.AreEqual(new[] { "space-elevator-input-grid-slot-2" }, FrameIds(view, "space-elevator-input-grid"));
            Assert.AreEqual(new[] { "space-elevator-output-grid-slot-1", "space-elevator-output-grid-slot-0" }, FrameIds(view, "space-elevator-output-grid"));
            simulate.Click(Find<VisualElement>(view, "space-elevator-output-grid-slot-0"));
            CollectionAssert.AreEqual(new[] { "LeftDown 0" }, BlockSlots.Issued);
        }
    }
}
