using Client.Game.InGame.DistanceCulling;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    public class DistanceCullingChunkTableTangentTest
    {
        [TestCase(4f, 20f, false)]
        [TestCase(4f, 20f, true)]
        [TestCase(32f, 120f, false)]
        [TestCase(32f, 120f, true)]
        public void AddingAtNegativeTangent_PreservesExistingTarget(float chunkSize, float radius, bool zAxis)
        {
            var table = new DistanceCullingChunkTable(chunkSize);
            var first = new FakeCullingTarget();
            var second = new FakeCullingTarget();
            var center = zAxis ? new Vector3(chunkSize / 2, 0, radius) : new Vector3(radius, 0, chunkSize / 2);

            // 先行登録を評価してから同じ接線チャンクへ追加する
            // Evaluate the first registration before adding to the same tangent chunk
            var position = zAxis ? new Vector3(1, 0, -3) : new Vector3(-3, 0, 1);
            using var firstRegistration = table.Add(CullingBounds.Point(position.x, position.z), first);
            table.UpdateForCenter(center, radius);
            using var secondRegistration = table.Add(CullingBounds.Point(zAxis ? 2 : -2, zAxis ? -2 : 2), second);
            Assert.AreEqual(0, first.OffCount);

            // 離脱と再接近で両登録が一度ずつ切り替わる
            // Both registrations toggle exactly once on departure and return
            var away = center + (zAxis ? Vector3.forward : Vector3.right) * (radius + chunkSize);
            table.UpdateForCenter(away, radius);
            Assert.AreEqual(false, first.Last);
            Assert.AreEqual(false, second.Last);
            table.UpdateForCenter(Vector3.zero, radius);
            Assert.AreEqual(true, first.Last);
            Assert.AreEqual(true, second.Last);
            Assert.AreEqual(1, first.OnCount);
            Assert.AreEqual(1, first.OffCount);
        }
    }
}
