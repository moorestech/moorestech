using System;
using System.Linq;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Special.Space;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Commands;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Core.Master;
using Game.Block.Interface.State;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// 発射台の本文と題の操作（Web RocketLaunchPadInventory.test.tsx の写せる 5 件。TS の型検査の 1 件は捨てる。D-13）
    /// Launch pad body and title action (the five portable cases of Web RocketLaunchPadInventory.test.tsx; the TS type-check case is discarded. D-13)
    /// </summary>
    public class RocketLaunchPadViewTest : SpecialScreenTestHost
    {
        /// <web-test file="src/features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx">a mismatched registry key shows pending detail without throwing</web-test>
        [Test]
        public void 発射台の詳細が無い種別では取得待ちを出し落ちない()
        {
            var view = Mount(new RocketLaunchPadBodyView(Kit(null)));
            view.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.PoleNetwork));
            Assert.IsTrue(IsShown(Find<Label>(view, "rocket-launch-pad-loading")));
            Assert.IsFalse(IsShown(Find<VisualElement>(view, "rocket-launch-pad-view")));
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx">waits for initial detail and renders when the topic arrives</web-test>
        [Test]
        public void 詳細の未着は取得待ちで届いたら本文を描く()
        {
            var view = Mount(new RocketLaunchPadBodyView(Kit(null)));
            view.Render(NoSlots, SpecialBlockDetailViewModel.Empty);
            Assert.IsTrue(IsShown(Find<Label>(view, "rocket-launch-pad-loading")));

            view.Render(ThreeSlots(), SpecialDetailSamples.Only(SpecialDetailSamples.Rocket(RocketLaunchPadPhase.AwaitingBuildCommand)));
            Assert.IsFalse(IsShown(Find<Label>(view, "rocket-launch-pad-loading")));
            Assert.IsTrue(IsShown(Find<VisualElement>(view, "rocket-launch-pad-view")));
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx">dispatches both commands from GamePanel.titleAction and hides them outside command waits</web-test>
        [Test]
        public void 題の操作は合図待ちの段階だけ出て押すと合図を送る()
        {
            var action = Mount(new RocketLaunchPadTitleActionView(Kit(null)));
            action.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.Rocket(RocketLaunchPadPhase.AwaitingBuildCommand)));
            simulate.FrameUpdate();
            simulate.Click(Find<Button>(action, "rocket-launch-pad-build"));
            action.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.Rocket(RocketLaunchPadPhase.AwaitingLaunchCommand)));
            Assert.IsFalse(IsShown(Find<Button>(action, "rocket-launch-pad-build")));
            simulate.Click(Find<Button>(action, "rocket-launch-pad-launch"));
            CollectionAssert.AreEqual(new[] { "rocket_launch_pad.command Build", "rocket_launch_pad.command Launch" }, Commands.Issued);

            action.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.Rocket(RocketLaunchPadPhase.Building)));
            Assert.IsFalse(IsShown(Find<Button>(action, "rocket-launch-pad-launch")));
            Assert.IsFalse(IsShown(action), "合図待ち以外は題の操作ごと出さない");
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx">%sの成功後topicが遅れても二度目の拒否を誤報しない</web-test>
        [TestCase(RocketCommand.Build, RocketLaunchPadPhase.AwaitingBuildCommand, RocketLaunchPadPhase.Building, "rocket-launch-pad-build")]
        [TestCase(RocketCommand.Launch, RocketLaunchPadPhase.AwaitingLaunchCommand, RocketLaunchPadPhase.ReadyToLaunch, "rocket-launch-pad-launch")]
        public void 成功の後に段階の更新が遅れても二度目の古い合図の拒否をトーストしない(RocketCommand command, RocketLaunchPadPhase phase, RocketLaunchPadPhase nextPhase, string testid)
        {
            var action = Mount(new RocketLaunchPadTitleActionView(Kit(null)));
            action.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.Rocket(phase)));
            simulate.FrameUpdate();
            simulate.Click(Find<Button>(action, testid));
            Commands.SetNextResult(new UiCommandRejected(UiCommandRejectReason.SpecialBlockCommandStale, command == RocketCommand.Build ? "rocket_command_not_awaiting_build" : "rocket_command_not_awaiting_launch"));
            simulate.Click(Find<Button>(action, testid));

            Assert.AreEqual(2, Commands.Issued.Count);
            CollectionAssert.IsEmpty(ShownToasts);
            action.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.Rocket(nextPhase)));
            Assert.IsFalse(IsShown(Find<Button>(action, testid)));
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/RocketLaunchPadInventory.test.tsx">renders DTO groups and preserves their absolute indices through real mouse gestures</web-test>
        [Test]
        public void 群を描き実際の押下でもサーバーの絶対添字を保つ()
        {
            var view = Mount(new RocketLaunchPadBodyView(Kit(LoadContent())));
            var slots = new[] { new SlotItem(new ItemId(3), 20), new SlotItem(new ItemId(4), 5), new SlotItem(new ItemId(5), 7), new SlotItem(new ItemId(1), 2) };
            var rocket = new RocketLaunchPadDetail(RocketLaunchPadPhase.AwaitingBuildCommand, RocketLaunchPadWaitReason.OutputFull, 0.375f, new[] { 2 }, new[] { 0 }, Array.Empty<InletSlotBinding>(), 3);
            view.Render(slots, SpecialDetailSamples.Only(rocket));
            simulate.FrameUpdate();

            Assert.AreEqual(0.375f, GaugeValue(view, "rocket-launch-pad-progress"), 0.001f);
            Assert.AreEqual(new UiText(RocketLaunchPadLogic.StatusKey(rocket.Phase, rocket.WaitReason)).Resolve(), TextOf(view, "rocket-launch-pad-status"));
            foreach (var (group, index) in new[] { ("material", 2), ("payload", 0), ("output", 3) })
            {
                var frame = Find<VisualElement>(view, $"rocket-launch-pad-{group}-slots-slot-{index}");
                var before = BlockSlots.Issued.Count;
                simulate.Click(frame);
                simulate.Click(frame, MouseButton.RightMouse);
                simulate.DoubleClick(frame);

                // 二度押しは押下も伴うので、並びではなく全行が同じ絶対添字であることを見る（前例 SlotFrameGestureTest）
                // A double click also emits presses, so check every line carries the same absolute index rather than the exact order (precedent SlotFrameGestureTest)
                var issued = BlockSlots.Issued.Skip(before).ToArray();
                Assert.AreEqual($"LeftDown {index}", issued[0]);
                CollectionAssert.Contains(issued, $"RightDown {index}");
                CollectionAssert.Contains(issued, $"DoubleClick {index}");
                Assert.IsTrue(issued.All(line => line.EndsWith($" {index}")), string.Join(", ", issued));
            }
        }

        private static SlotItem[] ThreeSlots()
        {
            return new[] { new SlotItem(ItemMaster.EmptyItemId, 0), new SlotItem(ItemMaster.EmptyItemId, 0), new SlotItem(ItemMaster.EmptyItemId, 0) };
        }
    }
}
