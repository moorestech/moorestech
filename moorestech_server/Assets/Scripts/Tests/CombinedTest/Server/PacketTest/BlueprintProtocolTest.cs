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
                var responses = packet.GetPacketResponse(payload, new PacketResponseContext(null));
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
                var responses = packet.GetPacketResponse(payload, new PacketResponseContext(null));
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
                var responses = packet.GetPacketResponse(payload, new PacketResponseContext(null));
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
                var responses = packet.GetPacketResponse(payload, new PacketResponseContext(null));
                return MessagePackSerializer.Deserialize<BlueprintResponse>(responses[0]);
            }

            #endregion
        }

        [Test]
        public void 同名BPをパケットで2件作成しGuid指定で片方だけパケット削除できるTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockBlueprint();

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // 同名BPをパケット経由で2件作成する（連番付与は無い）
            // Create two same-name blueprints via packets; no numbering suffix is applied
            var create1 = Send(BlueprintRequest.CreateCreateRequest("dup", new Vector3Int(0, 0, 0), new Vector3Int(5, 2, 5)));
            var create2 = Send(BlueprintRequest.CreateCreateRequest("dup", new Vector3Int(0, 0, 0), new Vector3Int(5, 2, 5)));
            var guid1 = Guid.Parse(create1.RegisteredGuidStr);
            var guid2 = Guid.Parse(create2.RegisteredGuidStr);

            // 片方をGuid指定でパケット削除すると、もう片方だけ残る
            // Deleting one by GUID via packet leaves only the other
            var deleteResponse = Send(BlueprintRequest.CreateDeleteRequest(guid1));
            Assert.IsTrue(deleteResponse.Success);
            Assert.AreEqual(1, deleteResponse.Blueprints.Count);
            Assert.AreEqual(guid2.ToString(), deleteResponse.Blueprints[0].BlueprintGuidStr);

            #region Internal

            BlueprintResponse Send(BlueprintRequest request)
            {
                var payload = MessagePackSerializer.Serialize(request);
                var responses = packet.GetPacketResponse(payload, new PacketResponseContext(null));
                return MessagePackSerializer.Deserialize<BlueprintResponse>(responses[0]);
            }

            #endregion
        }

        [Test]
        public void 同名ブループリントは連番なしでそのまま登録されGuidで区別される()
        {
            // 既存テストと同じ初期化でdatastoreを取得する
            // Use the same initialization as existing tests to get the datastore
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<IBlueprintDatastore>();

            var guid1 = datastore.Register(new BlueprintJsonObject("同じ名前", new List<BlueprintBlockJsonObject>(), Guid.NewGuid()));
            var guid2 = datastore.Register(new BlueprintJsonObject("同じ名前", new List<BlueprintBlockJsonObject>(), Guid.NewGuid()));

            // 名前は加工されず同名2件が共存し、Guidは異なる
            // Names are untouched; two same-name entries coexist with distinct GUIDs
            Assert.AreEqual(2, datastore.Blueprints.Count(b => b.Name == "同じ名前"));
            Assert.AreNotEqual(guid1, guid2);

            // Guidで片方だけ削除できる
            // Deleting by GUID removes exactly one
            Assert.IsTrue(datastore.Delete(guid1));
            Assert.AreEqual(1, datastore.Blueprints.Count(b => b.Name == "同じ名前"));
        }

        [Test]
        public void Guid欠損をロードしてもローダー内で補完しない()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<IBlueprintDatastore>();

            // Guid欠損は正規マイグレーションの責務であり、通常ロードは入力を加工しない
            // Missing GUIDs belong to the migration path; normal loading must not mutate the input
            var missingGuid = new BlueprintJsonObject("Guid欠損BP", new List<BlueprintBlockJsonObject>(), Guid.Empty);
            datastore.LoadBlueprints(new List<BlueprintJsonObject> { missingGuid });

            Assert.AreEqual(Guid.Empty, datastore.Blueprints[0].BlueprintGuid);
        }
    }
}
