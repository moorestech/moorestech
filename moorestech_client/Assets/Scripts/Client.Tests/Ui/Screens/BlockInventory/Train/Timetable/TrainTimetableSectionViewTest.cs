using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Train.Timetable;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.Screens.BlockInventory.Train.Timetable
{
    public class TrainTimetableSectionViewTest : ScreenTestHost
    {
        private TrainTestBodies _bodies;
        private ITrainInventoryPart _body;

        [SetUp]
        public void OpenTimetableTab()
        {
            // 本番どおり組んだ列車の本文を載せ、時刻表タブを開く
            // Mount the train body wired as in production and open the timetable tab
            _bodies = new TrainTestBodies(Toaster, BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            _body = _bodies.CreateTrainBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(_body.Root);
            _body.Attach((OpenTrainInventoryPanel)BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            simulate.Click(Find<Button>(_body.Root, "train-tab-timetable"));
        }

        [TearDown]
        public void Close()
        {
            _body.Detach();
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">blocks auto-run until an added stop is authoritative and sends toggles immediately</web-test>
        [Test]
        public void 追加が確定するまで自動運転ONを塞ぎ切替は即送る()
        {
            // 停車駅 0 件では ON が無効で、理由の文が出る
            // With no stops, ON is disabled and the reason text shows
            Publish(Ready(Unit1, false, NoStops));
            Assert.IsFalse(Find<Button>(_body.Root, "train-timetable-auto-run-on").enabledSelf);
            Assert.IsTrue(IsShown(_body.Root.Q<Label>("train-timetable-auto-run-unavailable")));

            // 追加は楽観表示でも ON を開けない。受理と公開で確定してから開く
            // An optimistic add does not open ON; it opens once acceptance and the publication confirm it
            simulate.Click(Find<Button>(_body.Root, "train-timetable-station-12_0_40-add"));
            Assert.IsNotNull(_body.Root.Q("train-timetable-stop-0"));
            Assert.IsFalse(Find<Button>(_body.Root, "train-timetable-auto-run-on").enabledSelf);
            _bodies.TimetableCommands.Accept();
            Publish(NorthOnly(Unit1));
            Assert.IsTrue(Find<Button>(_body.Root, "train-timetable-auto-run-on").enabledSelf);
            Assert.IsFalse(IsShown(_body.Root.Q<Label>("train-timetable-auto-run-unavailable")));

            // 押すと楽観更新なしで即送る
            // Pressing sends immediately without an optimistic update
            simulate.Click(Find<Button>(_body.Root, "train-timetable-auto-run-on"));
            CollectionAssert.AreEqual(new[] { true }, _bodies.TimetableCommands.SentAutoRuns);
            Assert.IsTrue(IsSelected(_body.Root, "train-timetable-auto-run-off"));

            // 自動運転 ON が公開された後は OFF を押すと false を送る
            // After auto-run ON is published, pressing OFF sends false
            _bodies.TimetableCommands.Accept();
            Publish(Ready(Unit1, true, Added(NoStops, North)));
            Assert.IsTrue(IsSelected(_body.Root, "train-timetable-auto-run-on"));
            simulate.Click(Find<Button>(_body.Root, "train-timetable-auto-run-off"));
            CollectionAssert.AreEqual(new[] { true, false }, _bodies.TimetableCommands.SentAutoRuns);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">disables moves past either end and follows remote changes when idle</web-test>
        [Test]
        public void 端を越える移動を塞ぎ待ちが無いときは外の変更に従う()
        {
            // 1 駅では上も下も無効
            // With one stop both up and down are disabled
            Publish(NorthOnly(Unit1));
            Assert.IsFalse(Find<Button>(_body.Root, "train-timetable-stop-0-up").enabledSelf);
            Assert.IsFalse(Find<Button>(_body.Root, "train-timetable-stop-0-down").enabledSelf);

            // 待ちが無ければ別の画面からの変更（2 駅）をそのまま映し、何も送らない
            // When idle, a change from elsewhere (two stops) shows as is and nothing is sent
            Publish(Ready(Unit1, false, Added(Added(NoStops, North), Unnamed)));
            Assert.IsNotNull(_body.Root.Q("train-timetable-stop-1"));
            Assert.IsTrue(Find<Button>(_body.Root, "train-timetable-stop-0-down").enabledSelf);
            Assert.IsFalse(Find<Button>(_body.Root, "train-timetable-stop-1-down").enabledSelf);
            Assert.AreEqual(0, _bodies.TimetableCommands.SentReplaces.Count);
            Assert.AreEqual(0, _bodies.TimetableCommands.SentAutoRuns.Count);
        }

        private void Publish(TrainTimetableReadyData data)
        {
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Ready(data));
            simulate.FrameUpdate();
        }
    }
}
