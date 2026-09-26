using System.Text.RegularExpressions;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using MessagePack;
using NUnit.Framework;
using Server.Protocol;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using Request = Server.Protocol.PacketResponse.SetTrainStationNameProtocol.SetTrainStationNameRequest;
using Response = Server.Protocol.PacketResponse.SetTrainStationNameProtocol.SetTrainStationNameResponse;
using FailureReason = Server.Protocol.PacketResponse.SetTrainStationNameProtocol.SetTrainStationNameFailureReason;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class SetTrainStationNameProtocolTest
    {
        [Test]
        public void SetsTrimmedNameAndNotifiesBlockState()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var block = TrainTestHelper.PlaceBlock(environment, ForUnitTestModBlockId.TestTrainStation, Vector3Int.zero, BlockDirection.North);
            var station = block.GetComponent<TrainStationComponent>();
            var notifications = 0;
            using var subscription = station.OnChangeBlockState.Subscribe(_ =>
            {
                Assert.AreEqual("北駅", station.StationName);
                notifications++;
            });

            // パケット往復で正規化と状態通知を確認する
            // Verify normalization and state notification through the packet round trip
            var response = Send(environment, new Request(Vector3Int.zero, "  北駅 \t"));
            Assert.IsTrue(response.Success);
            Assert.AreEqual(FailureReason.None, response.FailureReason);
            Assert.AreEqual("北駅", response.AppliedName);
            Assert.AreEqual("北駅", station.StationName);
            Assert.AreEqual(1, notifications);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t\r\n　")]
        public void RejectsEmptyNameWithoutMutationOrNotification(string name)
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var block = TrainTestHelper.PlaceBlock(environment, ForUnitTestModBlockId.TestTrainStation, Vector3Int.zero, BlockDirection.North);
            var station = block.GetComponent<TrainStationComponent>();
            station.SetStationName("既存駅");
            var notifications = 0;
            using var subscription = station.OnChangeBlockState.Subscribe(_ => notifications++);

            // 拒否時に既存名と購読者を変更しない
            // Rejections leave the existing name and subscribers unchanged
            ExpectRejection(FailureReason.EmptyName);
            var response = Send(environment, new Request(Vector3Int.zero, name));
            AssertRejected(response, FailureReason.EmptyName);
            Assert.AreEqual("既存駅", station.StationName);
            Assert.AreEqual(0, notifications);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RejectsCargoPlatforms(bool fluid)
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var blockId = fluid ? ForUnitTestModBlockId.TestTrainFluidPlatform : ForUnitTestModBlockId.TestTrainItemPlatform;
            TrainTestHelper.PlaceBlock(environment, blockId, Vector3Int.zero, BlockDirection.North);

            // 貨物と液体の両プラットフォームを駅名編集から除外する
            // Exclude both item and fluid platforms from station renaming
            ExpectRejection(FailureReason.NotTrainStation);
            var response = Send(environment, new Request(Vector3Int.zero, "北駅"));
            AssertRejected(response, FailureReason.NotTrainStation);
        }

        [Test]
        public void RejectsMissingBlock()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            ExpectRejection(FailureReason.BlockNotFound);
            var response = Send(environment, new Request(new Vector3Int(50, 0, 50), "北駅"));
            AssertRejected(response, FailureReason.BlockNotFound);
        }

        [Test]
        public void RejectsMissingPositionWithoutThrowing()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var request = new Request(Vector3Int.zero, "北駅") { Position = null };
            ExpectRejection(FailureReason.InvalidRequest);
            var response = Send(environment, request);
            AssertRejected(response, FailureReason.InvalidRequest);
        }

        private static void ExpectRejection(FailureReason reason)
        {
            LogAssert.Expect(LogType.Warning, new Regex($"\\[SetTrainStationName\\] rejected reason={reason}"));
        }

        private static void AssertRejected(Response response, FailureReason reason)
        {
            Assert.IsFalse(response.Success);
            Assert.AreEqual(reason, response.FailureReason);
            Assert.AreEqual(string.Empty, response.AppliedName);
        }

        private static Response Send(TrainTestEnvironment environment, Request request)
        {
            // 実ディスパッチャを使いタグ登録と応答番号も検証する
            // Use the actual dispatcher to check tag registration and response sequence
            request.SequenceId = 73;
            var payload = MessagePackSerializer.Serialize(request);
            var responses = environment.PacketResponseCreator.GetPacketResponse(payload, new PacketResponseContext(null));
            Assert.AreEqual(1, responses.Count);
            var response = MessagePackSerializer.Deserialize<Response>(responses[0]);
            Assert.AreEqual(request.Tag, response.Tag);
            Assert.AreEqual(request.SequenceId, response.SequenceId);
            return response;
        }
    }
}
