using System;
using Game.Block.Blocks.ElectricToGear;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class ElectricToGearOutputModeProtocolTest
    {
        private static readonly Vector3Int Pos = Vector3Int.zero;

        [Test]
        public void SetOutputModeViaPacket()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestElectricToGearGenerator, Pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var c = block.GetComponent<ElectricToGearGeneratorComponent>();

            // index 2 に切替えるリクエストを送る。
            // Send a request to switch to index 2.
            var response = Send(packet, new SetElectricToGearOutputModeRequest(Pos, 2));

            Assert.IsTrue(response.Success);
            Assert.AreEqual(2, response.AppliedIndex);
            Assert.AreEqual(2, c.SelectedIndex);

            // 範囲外 index は適用されず Success=false / FailureReason=InvalidIndex。状態は維持。
            // Out-of-range index is not applied → Success=false / FailureReason=InvalidIndex; state preserved.
            var response2 = Send(packet, new SetElectricToGearOutputModeRequest(Pos, 99));
            Assert.IsFalse(response2.Success);
            Assert.AreEqual(SetElectricToGearOutputModeFailureReason.InvalidIndex, response2.FailureReason);
            Assert.AreEqual(2, response2.AppliedIndex);
            Assert.AreEqual(2, c.SelectedIndex);
        }

        [Test]
        public void FailsGracefullyForMissingOrWrongBlock()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // ブロックが無い座標 → Success=false / BlockNotFound。
            // No block at the position → Success=false / BlockNotFound.
            var noBlock = Send(packet, new SetElectricToGearOutputModeRequest(new Vector3Int(5, 0, 0), 1));
            Assert.IsFalse(noBlock.Success);
            Assert.AreEqual(SetElectricToGearOutputModeFailureReason.BlockNotFound, noBlock.FailureReason);

            // ElectricToGear ではない別ブロックを置いて送る → Success=false / NotElectricToGear。
            // Place a different (non-ElectricToGear) block and send → Success=false / NotElectricToGear.
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.TestGearToElectricGenerator, new Vector3Int(8, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var wrongBlock = Send(packet, new SetElectricToGearOutputModeRequest(new Vector3Int(8, 0, 0), 1));
            Assert.IsFalse(wrongBlock.Success);
            Assert.AreEqual(SetElectricToGearOutputModeFailureReason.NotElectricToGear, wrongBlock.FailureReason);
        }

        private static SetElectricToGearOutputModeResponse Send(PacketResponseCreator packet, SetElectricToGearOutputModeRequest request)
        {
            var payload = MessagePackSerializer.Serialize(request);
            var responseBytes = packet.GetPacketResponseForTest(payload, new PacketResponseContext())[0];
            return MessagePackSerializer.Deserialize<SetElectricToGearOutputModeResponse>(responseBytes);
        }
    }
}
