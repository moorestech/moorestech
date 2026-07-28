using System;
using System.Collections.Generic;
using Game.Blueprint;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class BlueprintDatastoreTest
    {
        [Test]
        public void RegisterIssuesDistinctGuidsWithoutRenamingTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<IBlueprintDatastore>();

            // 同名登録でも名前は加工されず、Guidで区別される
            // Duplicate names are left untouched; GUIDs distinguish the entries
            var blueprint1 = CreateBlueprint("factory");
            var blueprint2 = CreateBlueprint("factory");
            var guid1 = datastore.Register(blueprint1);
            var guid2 = datastore.Register(blueprint2);

            Assert.AreEqual("factory", blueprint1.Name);
            Assert.AreEqual("factory", blueprint2.Name);
            Assert.AreNotEqual(guid1, guid2);
            Assert.AreEqual(guid1, blueprint1.BlueprintGuid);
            Assert.AreEqual(guid2, blueprint2.BlueprintGuid);
            Assert.AreEqual(2, datastore.Blueprints.Count);

            #region Internal

            BlueprintJsonObject CreateBlueprint(string name)
            {
                var block = new BlueprintBlockJsonObject(Vector3Int.zero, Guid.NewGuid().ToString(), 0, new Dictionary<string, string>());
                return new BlueprintJsonObject(name, new List<BlueprintBlockJsonObject> { block });
            }

            #endregion
        }

        [Test]
        public void DeleteTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<IBlueprintDatastore>();

            var guid = datastore.Register(new BlueprintJsonObject("target", new List<BlueprintBlockJsonObject>()));

            Assert.IsTrue(datastore.Delete(guid));
            Assert.AreEqual(0, datastore.Blueprints.Count);
            Assert.IsFalse(datastore.Delete(Guid.NewGuid()));
        }

        [Test]
        public void SaveLoadRoundTripTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<IBlueprintDatastore>();

            var settings = new Dictionary<string, string> { { "TestKey", "{\"a\":1}" } };
            var block = new BlueprintBlockJsonObject(new Vector3Int(1, 0, -2), System.Guid.NewGuid().ToString(), 3, settings);
            datastore.Register(new BlueprintJsonObject("roundtrip", new List<BlueprintBlockJsonObject> { block }));

            // セーブJSONを別Datastoreへ復元し一致確認
            // Extract save JSON and restore into a fresh datastore
            var saved = datastore.GetSaveJsonObject();
            var restored = new BlueprintDatastore();
            restored.LoadBlueprints(saved);

            Assert.AreEqual(1, restored.Blueprints.Count);
            var restoredBlock = restored.Blueprints[0].Blocks[0];
            Assert.AreEqual(new Vector3Int(1, 0, -2), restoredBlock.Offset);
            Assert.AreEqual(3, restoredBlock.Direction);
            Assert.AreEqual("{\"a\":1}", restoredBlock.Settings["TestKey"]);
        }

        [Test]
        public void BlueprintGuidはJsonシリアライズを経由しても保持される()
        {
            // BlueprintGuidStrはprivate setterのため、実際のセーブ経路であるNewtonsoft経由で
            // 復元できることを確認する（Registerの直接呼び出しだけでは検証できない）
            // BlueprintGuidStr has a private setter, so verify it round-trips through the actual
            // Newtonsoft save path (a direct Register call alone would not exercise this)
            var original = new BlueprintJsonObject("json-roundtrip", new List<BlueprintBlockJsonObject>());
            original.SetBlueprintGuid(Guid.NewGuid());

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<BlueprintJsonObject>(json);

            Assert.AreEqual(original.BlueprintGuid, restored.BlueprintGuid);
            Assert.AreEqual(original.Name, restored.Name);
        }
    }
}
