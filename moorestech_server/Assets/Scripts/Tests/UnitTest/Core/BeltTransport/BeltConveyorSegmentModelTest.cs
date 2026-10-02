using System;
using System.Collections.Generic;
using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;

namespace Tests.UnitTest.Core.BeltTransport
{
    // 前端位置のリストで素朴に再現したモデルと、乱数操作列の結果を毎操作で突き合わせる
    // Compare against a naive model holding front positions in a list after every step of a random operation sequence
    public class BeltConveyorSegmentModelTest
    {
        private const int OperationCount = 800;

        [TestCase(1, 11)]
        [TestCase(2, 23)]
        [TestCase(3, 37)]
        [TestCase(5, 41)]
        [TestCase(8, 59)]
        public void 乱数操作列でリストモデルと一致する(int capacity, int seed)
        {
            var random = new Random(seed);
            var segment = new BeltConveyorSegment(capacity, 0);
            var model = new List<(long serial, int front)>();
            long nextSerial = 1;
            var dequeuedCount = 0;

            for (var step = 0; step < OperationCount; step++)
            {
                var roll = random.Next(100);
                if (roll < 40) Enqueue();
                else if (roll < 85) AdvanceOneTick();
                else if (roll < 99) Dequeue();
                else Rebuild();
                AssertMatches(step);
            }

            // リングを何周も回したことを保証する
            // Ensure the ring wrapped around several times
            Assert.Greater(dequeuedCount, capacity * 5, "ring did not wrap enough");

            #region Internal

            void Enqueue()
            {
                // 搬入と同じく空きの範囲で進入距離を選び、さらに奥へ置く場合も混ぜる
                // Choose an entry length within the offer like a receive, sometimes placing it deeper
                var offer = GetLength(segment) - GetTotalLength(segment);
                if (offer <= 0) return;
                var length = random.Next(1, Math.Min(W, offer) + 1);
                var gap = random.Next(2) == 0 ? offer - length : random.Next(offer - length + 1);
                var serial = nextSerial++;
                EnqueueTail(segment, gap, MakeItem(serial));
                var front = model.Count == 0 ? gap : model[model.Count - 1].front + W + gap;
                model.Add((serial, front));
            }

            void AdvanceOneTick()
            {
                var speed = random.Next(BeltConstants.MaxSpeed + 1);
                var sent = GetOutputLength(segment, speed) > 0 && random.Next(2) == 0;
                Advance(segment, speed, sent);
                if (sent)
                {
                    // 先頭が消え、残りは全員そのまま速度分進む
                    // The head leaves and everyone else moves by the full speed
                    model.RemoveAt(0);
                    dequeuedCount++;
                    for (var i = 0; i < model.Count; i++) model[i] = (model[i].serial, model[i].front - speed);
                    return;
                }

                // 先頭は出口で止まり、後続は直前の後端で止まる
                // The head stops at the exit and each follower stops at the previous rear
                for (var i = 0; i < model.Count; i++)
                {
                    var limit = i == 0 ? 0 : model[i - 1].front + W;
                    model[i] = (model[i].serial, Math.Max(limit, model[i].front - speed));
                }
            }

            void Dequeue()
            {
                if (model.Count == 0) return;
                DequeueHead(segment);
                model.RemoveAt(0);
                dequeuedCount++;
            }

            void Rebuild()
            {
                // 再構築と同じく捕捉した状態を新しい走行列へ復元する
                // Restore the captured state into a fresh segment as a rebuild does
                var states = segment.CaptureItems();
                segment = new BeltConveyorSegment(capacity, 0);
                segment.RestoreItems(states);
            }

            void AssertMatches(int step)
            {
                var states = segment.CaptureItems();
                Assert.AreEqual(model.Count, states.Length, $"count at step {step}");
                for (var i = 0; i < states.Length; i++)
                {
                    Assert.AreEqual(model[i].serial, states[i].Item.ItemInstanceId.AsPrimitive(), $"item[{i}] at step {step}");
                    Assert.AreEqual(model[i].front, states[i].DistanceToExit, $"distance[{i}] at step {step}");
                }
                AssertStructure(segment);
            }

            #endregion
        }
    }
}
