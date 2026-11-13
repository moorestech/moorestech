using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.Screens.BlockInventory.Train.Timetable;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ComponentStyleAssert;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    public class TrainTimetableFlowTest : ScreenTestHost
    {
        private TrainTestBodies _bodies;
        private ITrainInventoryPart _body;

        [SetUp]
        public void OpenTimetableTab()
        {
            // Web のモック trainTimetable: 停車駅は北駅 1 つ、駅一覧は無名(-8,0,3)と北駅(12,0,40)
            // Web mock trainTimetable: one stop at North; stations are unnamed (-8,0,3) and North (12,0,40)
            _bodies = new TrainTestBodies(Toaster, BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            _body = _bodies.CreateTrainBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(_body.Root);
            _body.Attach((OpenTrainInventoryPanel)BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            Click("train-tab-timetable");
        }

        [TearDown]
        public void Close()
        {
            _body.Detach();
        }

        /// <web-test file="e2e/tests/train/timetable.spec.ts">駅の追加・上下移動・削除を即時反映し、再読込でも保持する</web-test>
        [Test]
        public void 追加移動削除を即時に映し開き直しても保つ()
        {
            Assert.IsNull(_body.Root.Q("train-timetable-apply"));

            // 追加→上→下→削除で保留解除
            // Add/up/down/remove each release pending
            var added = Added(NorthOnly(Unit1).Stops, Unnamed);
            Edit("train-timetable-station--8_0_3-add", added);
            Assert.IsNotNull(_body.Root.Q("train-timetable-stop-1"));
            var up = TimetableEditLogic.MoveStop(added, 1, StopMoveDirection.Up);
            Edit("train-timetable-stop-1-up", up);
            StringAssert.Contains("(-8, 0, 3)", TrainTestText.TextsUnder(StopLabel(0)));
            var down = TimetableEditLogic.MoveStop(up, 0, StopMoveDirection.Down);
            Edit("train-timetable-stop-0-down", down);
            StringAssert.Contains("(12, 0, 40)", TrainTestText.TextsUnder(StopLabel(0)));
            var removed = TimetableEditLogic.RemoveStop(down, 0);
            Edit("train-timetable-stop-0-remove", removed);
            Assert.IsNull(_body.Root.Q("train-timetable-stop-1"));

            // タブの往復と開き直し（Web の再読込）のどちらでも反映済みの並びが残る
            // The applied order survives both a tab round trip and reopening (Web reload)
            Click("train-tab-inventory");
            Click("train-tab-timetable");
            StringAssert.Contains("(-8, 0, 3)", TrainTestText.TextsUnder(StopLabel(0)));
            Reopen();
            Assert.IsNull(_body.Root.Q("train-timetable-stop-1"));
            StringAssert.Contains("(-8, 0, 3)", TrainTestText.TextsUnder(StopLabel(0)));

            // 最後の1件削除でON封鎖
            // Removing last stop blocks ON
            Edit("train-timetable-stop-0-remove", TimetableEditLogic.RemoveStop(removed, 0));
            Assert.IsNull(_body.Root.Q("train-timetable-stop-0"));
            Assert.IsFalse(Find<Button>(_body.Root, "train-timetable-auto-run-on").enabledSelf);
            Assert.AreEqual(5, _bodies.TimetableCommands.SentReplaces.Count);
        }

        /// <web-test file="e2e/tests/train/timetable.spec.ts">拒否された追加を巻き戻し、次の追加で復帰する</web-test>
        [Test]
        public void 拒否された追加を巻き戻し次の追加で戻る()
        {
            Click("train-timetable-station--8_0_3-add");
            LogAssert.Expect(LogType.Warning, new Regex(@"train_timetable\.replace failed: test"));
            _bodies.TimetableCommands.Reject(UiCommandRejectReason.TrainTimetableReplaceFailed, "test");
            simulate.FrameUpdate();
            AssertIdle();
            Assert.IsNull(_body.Root.Q("train-timetable-stop-1"));
            Assert.AreEqual(1, ShownToasts.Count);

            // 次の追加は通り、公開で 2 件になる
            // The next add goes through and the publication shows two stops
            Edit("train-timetable-station--8_0_3-add", Added(NorthOnly(Unit1).Stops, Unnamed));
            Assert.IsNotNull(_body.Root.Q("train-timetable-stop-1"));
            Assert.AreEqual(2, _bodies.TimetableCommands.SentReplaces.Count);
        }

        // 押す → 受理 → 公開 → 保留が解けたことを確かめる
        // Press, accept, publish, then confirm the pending state cleared
        private void Edit(string testid, IReadOnlyList<TimetableEditStop> published)
        {
            Click(testid);
            _bodies.TimetableCommands.Accept();
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Ready(Ready(Unit1, false, published)));
            simulate.FrameUpdate();
            AssertIdle();
        }

        // 閉じて同じ貨車を開き直す。alpha と同じく旧部分を Detach し、工場から作り直して Attach するので、タブは持ち物から始まる
        // Close and reopen the same car; as alpha does, detach the old part and Attach a new one from the factory, so the tab starts on inventory
        private void Reopen()
        {
            _body.Detach();
            _body.Root.RemoveFromHierarchy();
            _bodies.Panel.Value = BlockInventoryPanelState.Closed;
            _bodies.Panel.Value = BlockInventoryTestPanels.Train(BlockInventoryPanelError.None);
            _body = _bodies.CreateTrainBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(_body.Root);
            _body.Attach((OpenTrainInventoryPanel)BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            Assert.IsTrue(IsSelected(_body.Root, "train-tab-inventory"));
            Click("train-tab-timetable");
        }

        [Test]
        public void 端の移動ボタンは文字だけ従属色で淡くしない()
        {
            // Web の実測: 先頭の「上へ」は無効で文字 rgb(166,167,171)・opacity 1（画面の .list button:disabled）
            // Web measurement: the first "up" is disabled with text rgb(166,167,171) and opacity 1 (the screen's .list button:disabled)
            var up = Find<Button>(_body.Root, "train-timetable-stop-0-up");
            Assert.IsFalse(up.enabledSelf);
            AssertColor(Hex("#A6A7AB"), up.Q<Label>().resolvedStyle.color, "text");
            Assert.AreEqual(1f, up.resolvedStyle.opacity, 0.001f);
        }

        private void AssertIdle()
        {
            Assert.IsFalse(_body.Root.Q("train-timetable-section").ClassListContains(TrainTimetableSectionView.BusyClass));
        }

        private Label StopLabel(int index)
        {
            return _body.Root.Q<Label>($"train-timetable-stop-{index}-label");
        }

        private void Click(string testid)
        {
            simulate.Click(Find<Button>(_body.Root, testid));
            simulate.FrameUpdate();
        }
    }
}
