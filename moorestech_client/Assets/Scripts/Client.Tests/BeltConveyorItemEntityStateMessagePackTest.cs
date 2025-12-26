using System;
using Core.Master;
using Game.Entity.Interface;
using MessagePack;
using NUnit.Framework;

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
            var original = new BeltConveyorItemEntityStateMessagePack(new ItemId(1), 1, sourceGuid, goalGuid);

            // シリアライズ/デシリアライズを行う
            // Serialize/deserialize
            var bytes = MessagePackSerializer.Serialize(original);
            var restored = MessagePackSerializer.Deserialize<BeltConveyorItemEntityStateMessagePack>(bytes);

            Assert.AreEqual(1, restored.ItemId);
            Assert.AreEqual(1, restored.Count);
            Assert.AreEqual(sourceGuid, restored.SourceConnectorGuid);
            Assert.AreEqual(goalGuid, restored.GoalConnectorGuid);
        }
    }
}
