using System;
using Game.Block.Interface;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest.MachineRecipe
{
    public class MachineRecipeSelectionProtocolTest
    {
        private static readonly Vector3Int MachinePosition = new(30, 0, 30);

        [Test]
        public void ハンドシェイク前は変更を拒否する()
        {
            var (packet, _) = CreateEnvironment(false);

            var response = Send(packet,
                MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRequest(MachinePosition, ForUnitTestMachineRecipeId.MachineIdRecipe),
                new PacketResponseContext());

            Assert.AreEqual(MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.NotHandshaken, response.FailureReason);
        }

        [Test]
        public void 選択変更は適用したGUIDを返す()
        {
            var (packet, context) = CreateEnvironment(true);

            var response = Send(packet,
                MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRequest(MachinePosition, ForUnitTestMachineRecipeId.MachineIdRecipe), context);

            Assert.AreEqual(MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.None, response.FailureReason);
            Assert.AreEqual(ForUnitTestMachineRecipeId.MachineIdRecipe, response.GetAppliedRecipeGuid());
        }

        [Test]
        public void 未解放レシピは拒否して現在値を維持する()
        {
            var (packet, context) = CreateEnvironment(true);
            Send(packet, MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRequest(
                MachinePosition, ForUnitTestMachineRecipeId.MachineIdRecipe), context);

            var response = Send(packet, MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRequest(
                MachinePosition, ForUnitTestMachineRecipeId.LockedMachineIdRecipe), context);

            Assert.AreEqual(MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.RecipeLocked, response.FailureReason);
            Assert.AreEqual(ForUnitTestMachineRecipeId.MachineIdRecipe, response.GetAppliedRecipeGuid());
        }

        [Test]
        public void 遠隔地の機械変更を拒否する()
        {
            var (packet, context) = CreateEnvironment(true);
            var farPosition = new Vector3Int(10000, 0, 10000);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, farPosition,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var response = Send(packet, MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRequest(
                farPosition, ForUnitTestMachineRecipeId.MachineIdRecipe), context);

            Assert.AreEqual(MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.TooFar, response.FailureReason);
            Assert.IsNull(response.GetAppliedRecipeGuid());
        }

        private static (PacketResponseCreator packet, PacketResponseContext context) CreateEnvironment(bool handshake)
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, MachinePosition,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var context = new PacketResponseContext();
            if (handshake)
            {
                var request = new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(0, "recipe-test");
                packet.GetPacketResponse(MessagePackSerializer.Serialize(request), context);
            }
            return (packet, context);
        }

        private static MachineRecipeSelectionProtocol.MachineRecipeSelectionResponse Send(
            PacketResponseCreator packet,
            MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest request,
            PacketResponseContext context)
        {
            var responses = packet.GetPacketResponse(MessagePackSerializer.Serialize(request), context);
            Assert.AreEqual(1, responses.Count);
            return MessagePackSerializer.Deserialize<MachineRecipeSelectionProtocol.MachineRecipeSelectionResponse>(responses[0]);
        }
    }
}
