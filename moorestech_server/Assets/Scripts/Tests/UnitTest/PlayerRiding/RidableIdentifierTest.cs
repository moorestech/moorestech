using Game.PlayerRiding.Interface;
using MessagePack;
using NUnit.Framework;

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

            Assert.AreEqual(RidableType.TrainCar.AsPrimitive(), restored.RidableType);
            Assert.AreEqual("123456789012345", restored.TrainCarInstanceId);
        }

        [Test]
        public void TrainCarRidableIdentifier_EqualityAndHashCode_AreBasedOnInstanceId()
        {
            // 同じ TrainCarInstanceId を持つ識別子は等価で、HashCode も一致する
            // Identifiers with the same TrainCarInstanceId are equal and share a hash code.
            var a = new TrainCarRidableIdentifier(777L);
            var b = new TrainCarRidableIdentifier(777L);
            var c = new TrainCarRidableIdentifier(778L);

            Assert.AreEqual(RidableType.TrainCar, a.Type);
            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsFalse(a.Equals(c));
        }

        [Test]
        public void RidableIdentifierConverter_RoundTripsBetweenInterfaceAndMessagePack()
        {
            // IRidableIdentifier → MessagePack → IRidableIdentifier の往復で等価性が保たれる
            // Round trip IRidableIdentifier -> MessagePack -> IRidableIdentifier preserves equality.
            IRidableIdentifier original = new TrainCarRidableIdentifier(999L);

            var messagePack = original.ToMessagePack();
            var restored = RidableIdentifierConverter.FromMessagePack(messagePack);

            Assert.IsTrue(original.Equals(restored));
        }
    }
}
