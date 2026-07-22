using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
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
            var first = new RemoveOperationRecord(new List<RemovedBlockInfo>());
            var second = new RemoveOperationRecord(new List<RemovedBlockInfo>());
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
            var records = new List<RemoveOperationRecord>();
            for (var i = 0; i < 33; i++)
            {
                var record = new RemoveOperationRecord(new List<RemovedBlockInfo>());
                records.Add(record);
                history.Push(record);
            }

            // 33件Pushすると最初の1件だけが落ち、32件がLIFOで取り出せる
            // After 33 pushes only the first record is dropped; 32 pop in LIFO order
            for (var i = 32; i >= 1; i--)
            {
                Assert.IsTrue(history.TryPop(out var popped));
                Assert.AreSame(records[i], popped);
            }
            Assert.IsFalse(history.TryPop(out _));
        }
    }
}
