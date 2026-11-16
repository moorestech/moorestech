using System.Collections.Generic;
using Client.Localization;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit;
using Client.Ui.State.Commands;
using NUnit.Framework;
using UnityEngine;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 保留を解くのは時刻表の中身が替わった公開だけで、駅一覧・駅名・貨車だけの組み直しでは解けないことを確かめる（L1）
    /// Verifies that only a publication changing the timetable releases the hold, not a rebuild from stations, names or cars (L1)
    /// </summary>
    public class TrainTimetableEditSessionPublicationTest
    {
        private FakeTrainTimetableCommands _commands;
        private ManualTrainTimetableWaitTimer _timer;
        private TrainTimetableEditSession _session;
        private IReadOnlyList<TimetableEditStop> _server;

        [SetUp]
        public void SetUp()
        {
            Localize.Initialize();
            _commands = new FakeTrainTimetableCommands();
            _timer = new ManualTrainTimetableWaitTimer();
            _server = Added(NoStops, North);
            _session = new TrainTimetableEditSession(_commands, _timer, new UiCommandResultToaster(new GameUiToastFeed()), Ready(Unit1, false, _server));
        }

        [Test]
        public void 受理の後に駅一覧だけの組み直しが届いても保留を解かず時刻表の公開で解く()
        {
            var added = Added(_server, Unnamed);
            _session.Replace(added);
            _commands.Accept();
            Assert.IsTrue(_session.IsPending);

            // 駅一覧だけが替わった組み直し（時刻表は古いまま）。保留は残り、古い並びを正として送れない
            // A rebuild changing only the station list (timetable still old): the hold stays so edits cannot start from the old order
            var stationsOnly = new TrainTimetableReadyData(Unit1, false, 0, _server, new List<TrainTimetableStation> { North });
            _session.ReceiveServer(stationsOnly);
            Assert.IsTrue(_session.IsPending);
            Assert.AreSame(stationsOnly, _session.Server);

            // 駅名だけの更新（座標と端は同じ）でも解けない
            // A name-only update (same position and side) does not release either
            var renamed = new List<TimetableEditStop> { new(_server[0].Position, "改名", _server[0].Side) };
            _session.ReceiveServer(new TrainTimetableReadyData(Unit1, false, 0, renamed, new List<TrainTimetableStation> { North }));
            Assert.IsTrue(_session.IsPending);

            // 時刻表の中身が替わった公開で解く
            // A publication changing the timetable releases it
            _session.ReceiveServer(Ready(Unit1, false, added));
            Assert.IsFalse(_session.IsPending);
            CollectionAssert.AreEqual(added, _session.DisplayedStops);
        }

        [Test]
        public void 送信中に駅一覧だけの組み直しが届いても受理の時点で解かず時間切れを仕掛ける()
        {
            var added = Added(_server, Unnamed);
            _session.Replace(added);
            _session.ReceiveServer(new TrainTimetableReadyData(Unit1, false, 0, _server, new List<TrainTimetableStation> { North }));
            _commands.Accept();

            // 時刻表の公開はまだなので公開待ちのまま
            // No timetable publication yet, so it keeps waiting for one
            Assert.IsTrue(_session.IsPending);
            Assert.AreEqual(1, _timer.RequestedDelays.Count);

            // 自動運転の切替だけの公開は保留を解かず、時間切れも増えない
            // A publication that only flips auto-run keeps the hold and adds no timeout
            _session.ReceiveServer(new TrainTimetableReadyData(Unit1, true, 0, _server, new List<TrainTimetableStation> { North }));
            Assert.IsTrue(_session.IsPending);
            Assert.AreEqual(1, _timer.RequestedDelays.Count);

            // 並びの替わった公開で解ける
            // A publication changing the order releases it
            _session.ReceiveServer(Ready(Unit1, true, added));
            Assert.IsFalse(_session.IsPending);
        }
    }
}
