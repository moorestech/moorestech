using System;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using MessagePack;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class SetTrainPlatformTransferModeProtocolTest
    {
        [Test]
        public void SwitchItemPlatformToUnloadMode()
        {
            // 貨物アイテムプラットフォームを設置し、初期はLoadToTrainであることを前提とする
            // Place an item cargo platform; the initial mode is expected to be LoadToTrain
            var environment = TrainTestHelper.CreateEnvironment();
            var position = new Vector3Int(0, 0, 0);
            var block = TrainTestHelper.PlaceBlock(environment, ForUnitTestModBlockId.TestTrainItemPlatform, position, BlockDirection.North);
            Assert.IsNotNull(block);

            var transfer = block.GetComponent<TrainPlatformTransferComponent>();
            Assert.IsNotNull(transfer);
            Assert.AreEqual(TrainPlatformTransferComponent.TransferMode.LoadToTrain, transfer.Mode);

            // モード切替リクエストを送信する
            // Send the mode switch request
            var response = SendRequest(environment, position, TrainPlatformTransferComponent.TransferMode.UnloadToPlatform);

            // 成功応答とコンポーネント実体の両方が更新されることを確認する
            // Verify both the response and the underlying component reflect the new mode
            Assert.IsTrue(response.Success);
            Assert.AreEqual(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform, response.AppliedMode);
            Assert.AreEqual(SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeFailureReason.None, response.FailureReason);
            Assert.AreEqual(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform, transfer.Mode);
        }

        [Test]
        public void SwitchFluidPlatformToUnloadMode()
        {
            // 貨物液体プラットフォームでも同じ切替が成立することを確認する
            // The same switch must succeed for the fluid cargo platform
            var environment = TrainTestHelper.CreateEnvironment();
            var position = new Vector3Int(0, 0, 0);
            var block = TrainTestHelper.PlaceBlock(environment, ForUnitTestModBlockId.TestTrainFluidPlatform, position, BlockDirection.North);
            Assert.IsNotNull(block);

            var transfer = block.GetComponent<TrainPlatformTransferComponent>();
            Assert.IsNotNull(transfer);

            var response = SendRequest(environment, position, TrainPlatformTransferComponent.TransferMode.UnloadToPlatform);

            Assert.IsTrue(response.Success);
            Assert.AreEqual(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform, response.AppliedMode);
            Assert.AreEqual(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform, transfer.Mode);
        }

        [Test]
        public void ReturnsBlockNotFoundForEmptyPosition()
        {
            // ブロックが存在しない座標は失敗応答を返すこと
            // A position without any block must yield a failure response
            var environment = TrainTestHelper.CreateEnvironment();
            var emptyPosition = new Vector3Int(100, 0, 100);

            var response = SendRequest(environment, emptyPosition, TrainPlatformTransferComponent.TransferMode.UnloadToPlatform);

            Assert.IsFalse(response.Success);
            Assert.AreEqual(SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeFailureReason.BlockNotFound, response.FailureReason);
        }

        [Test]
        public void ReturnsNotTrainPlatformForOtherBlock()
        {
            // プラットフォームでないブロックには NotTrainPlatform を返す
            // Non-platform blocks must report NotTrainPlatform
            var environment = TrainTestHelper.CreateEnvironment();
            var position = new Vector3Int(50, 0, 50);
            environment.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            Assert.IsNotNull(block);

            var response = SendRequest(environment, position, TrainPlatformTransferComponent.TransferMode.UnloadToPlatform);

            Assert.IsFalse(response.Success);
            Assert.AreEqual(SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeFailureReason.NotTrainPlatform, response.FailureReason);
        }

        private static SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeResponse SendRequest(
            TrainTestEnvironment environment,
            Vector3Int position,
            TrainPlatformTransferComponent.TransferMode mode)
        {
            var request = new SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeRequest(position, mode);
            var payload = MessagePackSerializer.Serialize(request);
            var responseBytes = environment.PacketResponseCreator.GetPacketResponse(payload, new PacketResponseContext());

            Assert.AreEqual(1, responseBytes.Count);
            return MessagePackSerializer.Deserialize<SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeResponse>(responseBytes[0]);
        }
    }
}
