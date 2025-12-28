using System;
using Core.Master;
using Game.Entity.Interface;
using MessagePack;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    public class BeltConveyorItemEntityStateMessagePackTest
    {
        /// <summary>
        /// MessagePackの往復でConnectorGuidが保持されるか検証
        /// Verify ConnectorGuid is preserved through MessagePack round-trip
        /// </summary>
        [Test]
        public void RoundTripConnectorGuidTest()
        {
            // Guid付きのMessagePackを作成する
            // Create MessagePack with Guid
            var sourceGuid = Guid.NewGuid();
            var goalGuid = Guid.NewGuid();
            var blockPosition = new Vector3Int(3, 4, 5);
            var original = new BeltConveyorItemEntityStateMessagePack(new ItemId(1), 1, sourceGuid, goalGuid, 0.75f, blockPosition);

            // シリアライズ/デシリアライズを行う
            // Serialize/deserialize
            var bytes = MessagePackSerializer.Serialize(original);
            var restored = MessagePackSerializer.Deserialize<BeltConveyorItemEntityStateMessagePack>(bytes);

            Assert.AreEqual(1, restored.ItemId);
            Assert.AreEqual(1, restored.Count);
            Assert.AreEqual(sourceGuid, restored.SourceConnectorGuid);
            Assert.AreEqual(goalGuid, restored.GoalConnectorGuid);
            Assert.AreEqual(0.75f, restored.RemainingPercent);
            Assert.AreEqual(3, restored.BlockPosX);
            Assert.AreEqual(4, restored.BlockPosY);
            Assert.AreEqual(5, restored.BlockPosZ);
        }
    }
}
