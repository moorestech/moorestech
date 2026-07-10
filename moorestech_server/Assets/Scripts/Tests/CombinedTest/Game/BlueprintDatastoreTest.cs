using System.Collections.Generic;
using Game.Blueprint;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class BlueprintDatastoreTest
    {
        [Test]
        public void RegisterAndDuplicateNameTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<IBlueprintDatastore>();

            // 同名登録で連番が付与されることを確認
            // Duplicate names get numbered suffixes
            var name1 = datastore.Register(CreateBlueprint("factory"));
            var name2 = datastore.Register(CreateBlueprint("factory"));
            var name3 = datastore.Register(CreateBlueprint("factory"));

            Assert.AreEqual("factory", name1);
            Assert.AreEqual("factory (2)", name2);
            Assert.AreEqual("factory (3)", name3);
            Assert.AreEqual(3, datastore.Blueprints.Count);

            #region Internal

            BlueprintJsonObject CreateBlueprint(string name)
            {
                var block = new BlueprintBlockJsonObject(Vector3Int.zero, System.Guid.NewGuid().ToString(), 0, new Dictionary<string, string>());
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

            datastore.Register(new BlueprintJsonObject("target", new List<BlueprintBlockJsonObject>()));

            Assert.IsTrue(datastore.Delete("target"));
            Assert.AreEqual(0, datastore.Blueprints.Count);
            Assert.IsFalse(datastore.Delete("missing"));
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
    }
}
