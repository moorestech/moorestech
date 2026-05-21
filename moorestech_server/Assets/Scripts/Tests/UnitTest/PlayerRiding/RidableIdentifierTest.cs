using MessagePack;
using NUnit.Framework;
using Server.Util.MessagePack;

namespace Tests.UnitTest.PlayerRiding
{
    public class RidableIdentifierTest
    {
        [Test]
        public void RidableIdentifierMessagePack_CreateTrainCar_RoundTripsThroughMessagePack()
        {
            // 列車車両識別子を生成し、MessagePackシリアライズで往復しても値が保たれることを確認
            // A train-car identifier survives a MessagePack serialize/deserialize round trip.
            var original = RidableIdentifierMessagePack.CreateTrainCarMessage(123456789012345L);

            var bytes = MessagePackSerializer.Serialize(original);
            var restored = MessagePackSerializer.Deserialize<RidableIdentifierMessagePack>(bytes);

            Assert.AreEqual(RidableType.TrainCar, restored.RidableType);
            Assert.AreEqual("123456789012345", restored.TrainCarInstanceId);
        }
    }
}
