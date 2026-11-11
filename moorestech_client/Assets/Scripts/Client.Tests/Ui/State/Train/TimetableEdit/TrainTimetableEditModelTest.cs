using System.Text.RegularExpressions;
using Client.Localization;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.State.Train.TimetableEdit
{
    public class TrainTimetableEditModelTest
    {
        private FakeTrainSources _sources;
        private FakeTrainTimetableCommands _send;
        private ManualTrainTimetableWaitTimer _timer;
        private TrainTimetableEditModel _model;
        private TrainTimetableEditCommands _edit;

        [SetUp]
        public void SetUp()
        {
            // 拒否のトーストが文言を引くので、単独実行でも辞書を用意する
            // Refusal toasts look up text, so prepare the dictionary even when run alone
            Localize.Initialize();
            _sources = new FakeTrainSources();
            _send = new FakeTrainTimetableCommands();
            _timer = new ManualTrainTimetableWaitTimer();
            var toaster = new UiCommandResultToaster(new GameUiToastFeed());
            var state = new TrainTimetableEditState(_sources, _send, _timer, toaster);
            _model = new TrainTimetableEditModel(state);
            _edit = new TrainTimetableEditCommands(state, _send, toaster);
            state.Initialize();
            _model.Initialize();
        }

        [Test]
        public void 送った後に閉じると受理は何も出さず拒否だけトーストして時間切れを出さない()
        {
            // 閉じると源は使えないへ倒れる（本番は TrainTimetableModel が UIStateControl.OnStateChanged で組み直す。録画手順 6 で確かめる）
            // Closing turns the source unavailable (in production TrainTimetableModel rebuilds on UIStateControl.OnStateChanged; recording step 6 checks it)
            _sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            _edit.Add(Unnamed);
            _sources.SetTimetable(TrainTimetableViewState.Unavailable);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableEdit\] result after close discarded"));
            _send.Accept();
            Assert.IsEmpty(_timer.RequestedDelays);

            // 開き直して送り、また閉じてから拒否が戻ると、表示は捨てるがトーストの警告は出る（Web と同じ）
            // Reopen, send, close again; a late refusal discards the display but still raises the toaster warning (like the Web)
            _sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            _edit.Add(Unnamed);
            _sources.SetTimetable(TrainTimetableViewState.Unavailable);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[UiCommandResultToaster\] train_timetable\.replace failed"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableEdit\] result after close discarded"));
            _send.Reject(UiCommandRejectReason.TrainTimetableReplaceFailed, "late");
            LogAssert.NoUnexpectedReceived();
            Assert.IsInstanceOf<UnavailableTrainTimetableArea>(_model.Current.Value);
        }

        [Test]
        public void 列車が替わると保留を捨てて新しい列車の並びを出す()
        {
            // 列車 1 で追加して保留中のまま列車 2 が届く（Web の key=trainUnitId と同じく持ち越さない）
            // An add on train 1 is still pending when train 2 arrives (nothing carries over, like the Web key=trainUnitId)
            _sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            _edit.Add(Unnamed);
            Assert.IsTrue(Section().IsPending);
            _sources.SetTimetable(TrainTimetableViewState.Ready(Ready(Unit2, false, NoStops)));
            Assert.IsFalse(Section().IsPending);
            Assert.AreEqual(0, Section().Stops.Count);

            // 列車 1 の結果が後から戻っても列車 2 の表示は動かない
            // A late result for train 1 does not move train 2's display
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableEdit\] result after close discarded"));
            _send.Accept();
            Assert.AreEqual(0, Section().Stops.Count);
        }

        [Test]
        public void 時刻表が開いていない操作は拒否して送らない()
        {
            // 開いていないブロックへの操作と同じ理由で拒否し、Web と同じくトーストする（非良性。D-32）
            // Refused with the same reason as an operation on a block that is not open, and toasted like the Web (not benign; D-32)
            _sources.SetTimetable(TrainTimetableViewState.Unavailable);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableEdit\] rejected: block_not_open"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[UiCommandResultToaster\] train_timetable\.replace failed: block_not_open"));
            _edit.Add(North);
            Assert.IsEmpty(_send.SentReplaces);
        }

        private TrainTimetableSectionViewModel Section()
        {
            return ((ReadyTrainTimetableArea)_model.Current.Value).Section;
        }
    }
}
