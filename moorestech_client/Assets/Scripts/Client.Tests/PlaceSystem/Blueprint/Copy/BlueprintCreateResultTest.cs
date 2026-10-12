using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using NUnit.Framework;
using Server.Protocol.PacketResponse;

namespace Client.Tests.PlaceSystem
{
    public class BlueprintCreateResultTest
    {
        [TestCase(BlueprintFailureReason.NotUnlocked, BlueprintCreateFailure.NotUnlocked)]
        [TestCase(BlueprintFailureReason.InvalidName, BlueprintCreateFailure.InvalidName)]
        [TestCase(BlueprintFailureReason.EmptyArea, BlueprintCreateFailure.EmptyArea)]
        [TestCase(BlueprintFailureReason.InvalidRequest, BlueprintCreateFailure.InvalidRequest)]
        [TestCase(BlueprintFailureReason.UnknownOperation, BlueprintCreateFailure.Unknown)]
        public void 拒否理由を結果へ写す(BlueprintFailureReason reason, BlueprintCreateFailure expected)
        {
            var result = BlueprintCreateResult.Rejected(reason);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(expected, result.Failure);
            Assert.AreEqual(Guid.Empty, result.BlueprintGuid);
        }

        [Test]
        public void 通信失敗と成功を区別する()
        {
            Assert.AreEqual(BlueprintCreateFailure.RequestFailed, BlueprintCreateResult.RequestFailed().Failure);
            var guid = Guid.NewGuid();
            var result = BlueprintCreateResult.Succeeded(guid);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(guid, result.BlueprintGuid);
        }
    }
}
