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

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// Create/GetAll/Delete各Operationを検証
    /// Verifies the Create/GetAll/Delete operations of BlueprintProtocol.
    /// </summary>
    public class BlueprintProtocolTest
    {
        [Test]
        public void CreateGetAllDeleteFlowTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // Create:範囲内ブロックでBP登録
            // Create registers a blueprint from the area
            var createResponse = Send(BlueprintRequest.CreateCreateRequest("base", new Vector3Int(0, 0, 0), new Vector3Int(5, 2, 5)));
            Assert.IsTrue(createResponse.Success);
            Assert.AreEqual("base", createResponse.RegisteredName);
            Assert.AreEqual(1, createResponse.Blueprints.Count);
            Assert.AreEqual(1, createResponse.Blueprints[0].Blocks.Count);

            // GetAll: 登録済みBPが返る
            // GetAll returns registered blueprints
            var getAllResponse = Send(BlueprintRequest.CreateGetAllRequest());
            Assert.IsTrue(getAllResponse.Success);
            Assert.AreEqual(1, getAllResponse.Blueprints.Count);
            Assert.AreEqual("base", getAllResponse.Blueprints[0].Name);

            // Delete: 削除後は0件
            // Delete removes the blueprint
            var deleteResponse = Send(BlueprintRequest.CreateDeleteRequest("base"));
            Assert.IsTrue(deleteResponse.Success);
            Assert.AreEqual(0, deleteResponse.Blueprints.Count);

            #region Internal

            BlueprintResponse Send(BlueprintRequest request)
            {
                var payload = MessagePackSerializer.Serialize(request);
                var responses = packet.GetPacketResponseForTest(payload, new PacketResponseContext());
                return MessagePackSerializer.Deserialize<BlueprintResponse>(responses[0]);
            }

            #endregion
        }

        [Test]
        public void CreateFailuresTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 空範囲→EmptyArea
            // 空文字名→InvalidName
            // 削除対象無→NotFound
            // Empty area, empty name, and missing-delete failures
            var empty = Send(BlueprintRequest.CreateCreateRequest("x", new Vector3Int(50, 0, 50), new Vector3Int(55, 2, 55)));
            Assert.IsFalse(empty.Success);
            Assert.AreEqual(BlueprintFailureReason.EmptyArea, empty.FailureReason);

            var noName = Send(BlueprintRequest.CreateCreateRequest("", new Vector3Int(0, 0, 0), new Vector3Int(5, 2, 5)));
            Assert.IsFalse(noName.Success);
            Assert.AreEqual(BlueprintFailureReason.InvalidName, noName.FailureReason);

            var missingDelete = Send(BlueprintRequest.CreateDeleteRequest("missing"));
            Assert.IsFalse(missingDelete.Success);
            Assert.AreEqual(BlueprintFailureReason.NotFound, missingDelete.FailureReason);

            #region Internal

            BlueprintResponse Send(BlueprintRequest request)
            {
                var payload = MessagePackSerializer.Serialize(request);
                var responses = packet.GetPacketResponseForTest(payload, new PacketResponseContext());
                return MessagePackSerializer.Deserialize<BlueprintResponse>(responses[0]);
            }

            #endregion
        }
    }
}
