using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System.Collections.Generic;

namespace Client.Tests.BuildUndo
{
    public class BuildOperationHistoryTest
    {
        [Test]
        public void PushしたレコードがLIFO順でPopされる()
        {
            var history = new BuildOperationHistory();
            var first = new FakeOperationRecord();
            var second = new FakeOperationRecord();
            history.Push(first);
            history.Push(second);

            Assert.IsTrue(history.TryPop(out var popped1));
            Assert.AreSame(second, popped1);
            Assert.IsTrue(history.TryPop(out var popped2));
            Assert.AreSame(first, popped2);
            Assert.IsFalse(history.TryPop(out _));
        }

        [Test]
        public void 上限32を超えると最古のレコードが破棄される()
        {
            var history = new BuildOperationHistory();
            var records = new List<FakeOperationRecord>();
            for (var i = 0; i < 33; i++)
            {
                var record = new FakeOperationRecord();
                records.Add(record);
                history.Push(record);
            }

            // 最古1件だけ落ち32件がLIFOで残る
            // Only the oldest drops; 32 remain in LIFO order
            for (var i = 32; 1 <= i; i--)
            {
                Assert.IsTrue(history.TryPop(out var popped));
                Assert.AreSame(records[i], popped);
            }
            Assert.IsFalse(history.TryPop(out _));
        }

        // 履歴の入出力順のみ検証するためのフェイクレコード
        // Fake record used only to verify push/pop ordering
        private class FakeOperationRecord : IBuildOperationRecord
        {
            public UniTask UndoAsync(BlockGameObjectDataStore blockGameObjectDataStore)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
