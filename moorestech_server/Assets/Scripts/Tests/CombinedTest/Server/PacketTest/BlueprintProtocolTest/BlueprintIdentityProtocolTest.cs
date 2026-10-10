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
    public class BlueprintIdentityProtocolTest
    {

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
                var responses = packet.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1));
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

            var guid1 = datastore.Register(new BlueprintJsonObject("同じ名前", new List<BlueprintBlockJsonObject>(), new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), Guid.NewGuid()));
            var guid2 = datastore.Register(new BlueprintJsonObject("同じ名前", new List<BlueprintBlockJsonObject>(), new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), Guid.NewGuid()));

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
            var missingGuid = new BlueprintJsonObject("Guid欠損BP", new List<BlueprintBlockJsonObject>(), new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), Guid.Empty);
            datastore.LoadBlueprints(new List<BlueprintJsonObject> { missingGuid });

            Assert.AreEqual(Guid.Empty, datastore.Blueprints[0].BlueprintGuid);
        }
    }
}
