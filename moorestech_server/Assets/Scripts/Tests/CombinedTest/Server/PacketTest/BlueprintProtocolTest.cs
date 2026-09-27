using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Blueprint;
using Game.Context;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
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
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockBlueprint();

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // Create:範囲内ブロックでBP登録。応答は発行されたGuid
            // Create registers a blueprint from the area; the response carries the issued GUID
            var createResponse = Send(BlueprintRequest.CreateCreateRequest("base", new Vector3Int(0, 0, 0), new Vector3Int(5, 2, 5)));
            Assert.IsTrue(createResponse.Success);
            var registeredGuid = Guid.Parse(createResponse.RegisteredGuidStr);
            Assert.AreNotEqual(Guid.Empty, registeredGuid);
            Assert.AreEqual(1, createResponse.Blueprints.Count);
            Assert.AreEqual(1, createResponse.Blueprints[0].Blocks.Count);
            Assert.AreEqual(registeredGuid.ToString(), createResponse.Blueprints[0].BlueprintGuidStr);

            // GetAll: 登録済みBPが返る
            // GetAll returns registered blueprints
            var getAllResponse = Send(BlueprintRequest.CreateGetAllRequest());
            Assert.IsTrue(getAllResponse.Success);
            Assert.AreEqual(1, getAllResponse.Blueprints.Count);
            Assert.AreEqual("base", getAllResponse.Blueprints[0].Name);

            // Delete: Guid指定で削除後は0件
            // Delete removes the blueprint by GUID
            var deleteResponse = Send(BlueprintRequest.CreateDeleteRequest(registeredGuid));
            Assert.IsTrue(deleteResponse.Success);
            Assert.AreEqual(0, deleteResponse.Blueprints.Count);

            #region Internal

            BlueprintResponse Send(BlueprintRequest request)
            {
                var payload = MessagePackSerializer.Serialize(request);
                var responses = packet.GetPacketResponse(payload, Tests.Util.BoundPacketContext.Bind(1));
                return MessagePackSerializer.Deserialize<BlueprintResponse>(responses[0]);
            }

            #endregion
        }

        [Test]
        public void 未解放時はCreateとDeleteが拒否されGetAllは成功する()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // 未解放: Create/DeleteはNotUnlocked、GetAllは読み取り専用のため成功
            // Locked: Create/Delete fail with NotUnlocked while the read-only GetAll succeeds
            var createResponse = Send(BlueprintRequest.CreateCreateRequest("base", new Vector3Int(0, 0, 0), new Vector3Int(5, 2, 5)));
            Assert.IsFalse(createResponse.Success);
            Assert.AreEqual(BlueprintFailureReason.NotUnlocked, createResponse.FailureReason);

            var deleteResponse = Send(BlueprintRequest.CreateDeleteRequest(Guid.NewGuid()));
            Assert.IsFalse(deleteResponse.Success);
            Assert.AreEqual(BlueprintFailureReason.NotUnlocked, deleteResponse.FailureReason);

            var getAllResponse = Send(BlueprintRequest.CreateGetAllRequest());
            Assert.IsTrue(getAllResponse.Success);

            // 解放後はCreateが通る
            // After unlocking, Create succeeds
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockBlueprint();
            var unlockedCreate = Send(BlueprintRequest.CreateCreateRequest("base", new Vector3Int(0, 0, 0), new Vector3Int(5, 2, 5)));
            Assert.IsTrue(unlockedCreate.Success);

            #region Internal

            BlueprintResponse Send(BlueprintRequest request)
            {
                var payload = MessagePackSerializer.Serialize(request);
                var responses = packet.GetPacketResponse(payload, Tests.Util.BoundPacketContext.Bind(1));
                return MessagePackSerializer.Deserialize<BlueprintResponse>(responses[0]);
            }

            #endregion
        }

        [Test]
        public void ToJsonObjectはBlueprintGuidを保持するTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockBlueprint();

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // MessagePack→JsonObject変換後もGuidが失われないことを確認
            // ToJsonObject must not drop the GUID carried by the MessagePack DTO
            var createResponse = Send(BlueprintRequest.CreateCreateRequest("base", new Vector3Int(0, 0, 0), new Vector3Int(5, 2, 5)));
            var registeredGuid = Guid.Parse(createResponse.RegisteredGuidStr);
            var jsonObject = createResponse.Blueprints[0].ToJsonObject();
            Assert.AreEqual(registeredGuid, jsonObject.BlueprintGuid);

            #region Internal

            BlueprintResponse Send(BlueprintRequest request)
            {
                var payload = MessagePackSerializer.Serialize(request);
                var responses = packet.GetPacketResponse(payload, Tests.Util.BoundPacketContext.Bind(1));
                return MessagePackSerializer.Deserialize<BlueprintResponse>(responses[0]);
            }

            #endregion
        }

        [Test]
        public void CreateFailuresTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockBlueprint();

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

            var missingDelete = Send(BlueprintRequest.CreateDeleteRequest(Guid.NewGuid()));
            Assert.IsFalse(missingDelete.Success);
            Assert.AreEqual(BlueprintFailureReason.NotFound, missingDelete.FailureReason);

            #region Internal

            BlueprintResponse Send(BlueprintRequest request)
            {
                var payload = MessagePackSerializer.Serialize(request);
                var responses = packet.GetPacketResponse(payload, Tests.Util.BoundPacketContext.Bind(1));
                return MessagePackSerializer.Deserialize<BlueprintResponse>(responses[0]);
            }

            #endregion
        }
    }
}
