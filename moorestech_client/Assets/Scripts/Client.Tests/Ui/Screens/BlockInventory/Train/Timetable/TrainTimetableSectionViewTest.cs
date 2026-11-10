using System;
using Client.Localization;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Screens.BlockInventory.Train.Timetable;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.Screens.BlockInventory.Train.Timetable
{
    public class TrainTimetableSectionViewTest : ScreenTestHost
    {
        private FakeTrainSources _sources;
        private FakeTrainTimetableCommands _send;
        private TrainTimetableAreaView _view;
        private IDisposable _render;

        [SetUp]
        public void MountTimetableArea()
        {
            // 本物の編集の器・Model・コマンドを偽の源と偽の送信の上に組み、Presenter の代わりに Current を Render へ流す
            // Real edit holder, model and commands over fake source and sender; Current flows into Render in place of the Presenter
            _sources = new FakeTrainSources();
            _send = new FakeTrainTimetableCommands();
            var state = new TrainTimetableEditState(_sources, _send, new ManualTrainTimetableWaitTimer(), Toaster);
            var model = new TrainTimetableEditModel(state);
            var commands = new TrainTimetableEditCommands(state, _send, Toaster);
            state.Initialize();
            model.Initialize();
            _view = Mount(new TrainTimetableAreaView(commands));
            _render = model.Current.Subscribe(area => _view.Render(area, Localize.Get(LocalizationKeys.Ui.BlockInventory.TimetableUnnamedStation)));
        }

        [TearDown]
        public void Unsubscribe()
        {
            _render.Dispose();
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">blocks auto-run until an added stop is authoritative and sends toggles immediately</web-test>
        [Test]
        public void 追加が確定するまで自動運転ONを塞ぎ切替は即送る()
        {
            // 停車駅 0 件では ON が無効で、理由の文が出る
            // With no stops, ON is disabled and the reason text shows
            Publish(Ready(Unit1, false, NoStops));
            Assert.IsFalse(Find<Button>(_view, "train-timetable-auto-run-on").enabledSelf);
            Assert.IsTrue(IsShown(_view.Q<Label>("train-timetable-auto-run-unavailable")));

            // 追加は楽観表示でも ON を開けない。受理と公開で確定してから開く
            // An optimistic add does not open ON; it opens once acceptance and the publication confirm it
            simulate.Click(Find<Button>(_view, "train-timetable-station-12_0_40-add"));
            Assert.IsNotNull(_view.Q("train-timetable-stop-0"));
            Assert.IsFalse(Find<Button>(_view, "train-timetable-auto-run-on").enabledSelf);
            _send.Accept();
            Publish(NorthOnly(Unit1));
            Assert.IsTrue(Find<Button>(_view, "train-timetable-auto-run-on").enabledSelf);
            Assert.IsFalse(IsShown(_view.Q<Label>("train-timetable-auto-run-unavailable")));

            // 押すと楽観更新なしで即送る
            // Pressing sends immediately without an optimistic update
            simulate.Click(Find<Button>(_view, "train-timetable-auto-run-on"));
            CollectionAssert.AreEqual(new[] { true }, _send.SentAutoRuns);
            Assert.IsTrue(IsSelected(_view, "train-timetable-auto-run-off"));

            // 自動運転 ON が公開された後は OFF を押すと false を送る
            // After auto-run ON is published, pressing OFF sends false
            _send.Accept();
            Publish(Ready(Unit1, true, Added(NoStops, North)));
            Assert.IsTrue(IsSelected(_view, "train-timetable-auto-run-on"));
            simulate.Click(Find<Button>(_view, "train-timetable-auto-run-off"));
            CollectionAssert.AreEqual(new[] { true, false }, _send.SentAutoRuns);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">disables moves past either end and follows remote changes when idle</web-test>
        [Test]
        public void 端を越える移動を塞ぎ待ちが無いときは外の変更に従う()
        {
            // 1 駅では上も下も無効
            // With one stop both up and down are disabled
            Publish(NorthOnly(Unit1));
            Assert.IsFalse(Find<Button>(_view, "train-timetable-stop-0-up").enabledSelf);
            Assert.IsFalse(Find<Button>(_view, "train-timetable-stop-0-down").enabledSelf);

            // 待ちが無ければ別の画面からの変更（2 駅）をそのまま映し、何も送らない
            // When idle, a change from elsewhere (two stops) shows as is and nothing is sent
            Publish(Ready(Unit1, false, Added(Added(NoStops, North), Unnamed)));
            Assert.IsNotNull(_view.Q("train-timetable-stop-1"));
            Assert.IsTrue(Find<Button>(_view, "train-timetable-stop-0-down").enabledSelf);
            Assert.IsFalse(Find<Button>(_view, "train-timetable-stop-1-down").enabledSelf);
            Assert.AreEqual(0, _send.SentReplaces.Count);
            Assert.AreEqual(0, _send.SentAutoRuns.Count);
        }

        private void Publish(TrainTimetableReadyData data)
        {
            _sources.SetTimetable(TrainTimetableViewState.Ready(data));
            simulate.FrameUpdate();
        }
    }
}
