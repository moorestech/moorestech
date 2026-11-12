using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Localization;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.TimetableEdit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.State.Train.TimetableEdit
{
    public class TrainTimetableEditSessionTest
    {
        private FakeTrainTimetableCommands _commands;
        private ManualTrainTimetableWaitTimer _timer;
        private TrainTimetableEditSession _session;
        private IReadOnlyList<TimetableEditStop> _server;

        [SetUp]
        public void SetUp()
        {
            // 拒否のトーストが文言を引くので、単独実行でも辞書を用意する
            // Refusal toasts look up text, so prepare the dictionary even when run alone
            Localize.Initialize();
            _commands = new FakeTrainTimetableCommands();
            _timer = new ManualTrainTimetableWaitTimer();
            _server = Added(NoStops, North);
            _session = new TrainTimetableEditSession(_commands, _timer, new UiCommandResultToaster(new GameUiToastFeed()), Ready(Unit1, false, _server));
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">immediately sends each add, move and remove without an Apply button</web-test>
        [Test]
        public void 追加移動削除を適用ボタンなしで即送る()
        {
            // 追加は即送信され、表示も即替わる
            // An add is sent at once and shows at once
            var added = Added(_server, Unnamed);
            _session.Replace(added);
            Assert.AreEqual(1, _commands.SentReplaces.Count);
            Assert.AreEqual(2, _session.DisplayedStops.Count);
            Assert.IsTrue(_session.IsPending);

            // 保留解除後に移動・削除を1回ずつ送る
            // After release, move and remove send once each
            _commands.Accept();
            _session.ReceiveServer(Ready(Unit1, false, added));
            Assert.IsFalse(_session.IsPending);
            var moved = TimetableEditLogic.MoveStop(added, 1, StopMoveDirection.Up);
            _session.Replace(moved);
            _commands.Accept();
            _session.ReceiveServer(Ready(Unit1, false, moved));
            _session.Replace(TimetableEditLogic.RemoveStop(moved, 0));
            Assert.AreEqual(3, _commands.SentReplaces.Count);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">waits for publication after acceptance so the next edit cannot use stale stops</web-test>
        [Test]
        public void 受理後も公開が届くまで次の編集を受けない()
        {
            var added = Added(_server, Unnamed);
            _session.Replace(added);
            _commands.Accept();
            Assert.IsTrue(_session.IsPending);
            Assert.AreEqual(new[] { TimeSpan.FromMilliseconds(5000) }, _timer.RequestedDelays);

            // 保留中の編集は警告して捨てる
            // An edit while pending is dropped with a warning
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableEdit\] ignored"));
            _session.Replace(TimetableEditLogic.RemoveStop(added, 0));
            Assert.AreEqual(1, _commands.SentReplaces.Count);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">waits for acceptance even when the publication arrives first</web-test>
        [Test]
        public void 公開が先に届いても受理まで保留する()
        {
            var added = Added(_server, Unnamed);
            _session.Replace(added);
            _session.ReceiveServer(Ready(Unit1, false, added));
            Assert.IsTrue(_session.IsPending);

            // 受理の時点でサーバーが同じ並びなら時間切れを待たずに解く
            // On acceptance with a matching server order, release without waiting for the timeout
            _commands.Accept();
            Assert.IsFalse(_session.IsPending);
            Assert.IsEmpty(_timer.RequestedDelays);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">reverts rejected edits to the newest authority and allows another edit</web-test>
        [Test]
        public void 拒否は最新のサーバーの並びへ戻し次の編集を受ける()
        {
            _session.Replace(Added(_server, Unnamed));
            var remote = Added(NoStops, Unnamed);
            _session.ReceiveServer(Ready(Unit1, false, remote));

            // 拒否で保留を解き、表示は届いた最新の並び
            // Rejection releases the pending edit and shows the newest published order
            // ログ 1 行に理由と診断（A-260）
            // One log line holds reason and diagnostic (A-260)
            LogAssert.Expect(LogType.Warning, new Regex(@"\[UiCommandResultToaster\] train_timetable\.replace failed: replace_failed \(busy\)$"));
            _commands.Reject(UiCommandRejectReason.TrainTimetableReplaceFailed, "busy");
            Assert.IsFalse(_session.IsPending);
            CollectionAssert.AreEqual(remote, _session.DisplayedStops);
            _session.Replace(TimetableEditLogic.RemoveStop(remote, 0));
            Assert.AreEqual(2, _commands.SentReplaces.Count);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">releases a missing publication after the existing transport timeout</web-test>
        [Test]
        public void 公開が来なければ時間切れで解く()
        {
            _session.Replace(Added(_server, Unnamed));
            _commands.Accept();
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableEdit\] publication did not arrive"));
            _timer.Elapse();
            Assert.IsFalse(_session.IsPending);
            Assert.AreEqual(1, _session.DisplayedStops.Count);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableImmediate.test.ts">does not defer an edit or refresh a different train after closing</web-test>
        [Test]
        public void 閉じた後に戻った結果は表示を動かさない()
        {
            _session.Replace(Added(_server, Unnamed));
            var displayedAtClose = _session.DisplayedStops;
            _session.Close();

            // 閉じた後の受理は捨て時間切れも無し
            // Post-close acceptance dropped, no timeout
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainTimetableEdit\] result after close discarded"));
            _commands.Accept();
            Assert.AreSame(displayedAtClose, _session.DisplayedStops);
            Assert.IsEmpty(_timer.RequestedDelays);
        }
    }
}
