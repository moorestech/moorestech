using System.Collections.Generic;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.Train.Logic;
using Game.Train.RailGraph;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Train
{
    /// <summary>
    /// Web の timetableEditLogic.test.ts の写し（L0）
    /// Port of Web timetableEditLogic.test.ts (L0)
    /// </summary>
    public class TimetableEditLogicTest
    {
        private static readonly TrainTimetableStation A = new(new Vector3Int(1, 0, 1), "A");
        private static readonly TrainTimetableStation B = new(new Vector3Int(2, 0, 2), "");
        private static readonly TimetableEditStop AStop = new(A.Position, A.Name, StationNodeSide.Back);
        private static readonly TimetableEditStop BStop = new(B.Position, B.Name, StationNodeSide.Back);

        /// <web-test file="src/features/blockInventory/train/timetableEditLogic.test.ts">追加・削除・上下移動は新しいdraftを返し元を変えない</web-test>
        [Test]
        public void 追加削除上下移動は新しい一覧を返し元を変えない()
        {
            var d0 = TimetableEditLogic.AddStop(new List<TimetableEditStop>(), A);
            var d1 = TimetableEditLogic.AddStop(d0, B);
            Assert.AreEqual(1, d0.Count);
            Assert.AreEqual(A.Position, d1[0].Position);
            Assert.AreEqual(B.Position, d1[1].Position);
            AssertStop(BStop, TimetableEditLogic.MoveStop(d1, 1, StopMoveDirection.Up)[0]);

            // 端を越える移動は警告を出して同じインスタンスを返す
            // Moves past either end warn and return the same instance
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("move ignored"));
            Assert.AreSame(d1, TimetableEditLogic.MoveStop(d1, 0, StopMoveDirection.Up));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("move ignored"));
            Assert.AreSame(d1, TimetableEditLogic.MoveStop(d1, 1, StopMoveDirection.Down));

            var removed = TimetableEditLogic.RemoveStop(d1, 0);
            Assert.AreEqual(1, removed.Count);
            AssertStop(BStop, removed[0]);
        }

        /// <web-test file="src/features/blockInventory/train/timetableEditLogic.test.ts">同じ駅を2回入れられる（循環で2度停まる時刻表を許す）</web-test>
        [Test]
        public void 同じ駅を2回入れられる()
        {
            var draft = TimetableEditLogic.AddStop(TimetableEditLogic.AddStop(new List<TimetableEditStop>(), A), A);
            Assert.AreEqual(2, draft.Count);
        }

        /// <web-test file="src/features/blockInventory/train/timetableEditLogic.test.ts">停車駅が無いと自動運転ONにできない</web-test>
        [Test]
        public void 停車駅が無いと自動運転をONにできない()
        {
            Assert.IsFalse(TimetableEditLogic.CanEnableAutoRun(new List<TimetableEditStop>()));
            Assert.IsTrue(TimetableEditLogic.CanEnableAutoRun(new[] { AStop }));
        }

        /// <web-test file="src/features/blockInventory/train/timetableEditLogic.test.ts">適用ペイロードは座標と端を送る</web-test>
        [Test]
        public void 適用の送信形は座標と端を送る()
        {
            CollectionAssert.AreEqual(
                new[] { new TimetableReplaceStop(new Vector3Int(1, 0, 1), StationNodeSide.Back), new TimetableReplaceStop(new Vector3Int(2, 0, 2), StationNodeSide.Back) },
                TimetableEditLogic.ToReplacePayload(new[] { AStop, BStop }));
        }

        /// <web-test file="src/features/blockInventory/train/timetableEditLogic.test.ts">adds new stops on the fixed UI side (back) and keeps existing sides</web-test>
        [Test]
        public void 新しい停車駅はUI固定の後端で足し既存の端は保つ()
        {
            var existing = new TimetableEditStop(new Vector3Int(1, 0, 2), "A", StationNodeSide.Front);
            var draft = TimetableEditLogic.AddStop(new[] { existing }, new TrainTimetableStation(new Vector3Int(5, 0, 6), "B"));
            CollectionAssert.AreEqual(
                new[] { new TimetableReplaceStop(new Vector3Int(1, 0, 2), StationNodeSide.Front), new TimetableReplaceStop(new Vector3Int(5, 0, 6), StationNodeSide.Back) },
                TimetableEditLogic.ToReplacePayload(draft));
        }

        /// <web-test file="src/features/blockInventory/train/timetableEditLogic.test.ts">formats named and unnamed station labels with all coordinates</web-test>
        [Test]
        public void 名前あり名前なしの駅名を全座標つきで組み順序の一致は座標と端で見る()
        {
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.TimetableStationLabel, new[] { "name", "A", "x", "1", "y", "0", "z", "1" }, TimetableEditLogic.StationLabel(A, "駅"));
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.TimetableStationLabel, new[] { "name", "駅", "x", "2", "y", "0", "z", "2" }, TimetableEditLogic.StationLabel(B, "駅"));
            Assert.IsFalse(TimetableEditLogic.SameStopOrder(new[] { AStop, BStop }, new[] { BStop, AStop }));
            Assert.IsTrue(TimetableEditLogic.SameStopOrder(new[] { AStop }, new[] { new TimetableEditStop(AStop.Position, "renamed", AStop.Side) }));
            Assert.IsFalse(TimetableEditLogic.SameStopOrder(new[] { AStop }, new[] { new TimetableEditStop(AStop.Position, AStop.Name, StationNodeSide.Front) }));
        }

        private static void AssertStop(TimetableEditStop expected, TimetableEditStop actual)
        {
            Assert.AreEqual(expected.Position, actual.Position, "position");
            Assert.AreEqual(expected.Name, actual.Name, "name");
            Assert.AreEqual(expected.Side, actual.Side, "side");
        }
    }
}
