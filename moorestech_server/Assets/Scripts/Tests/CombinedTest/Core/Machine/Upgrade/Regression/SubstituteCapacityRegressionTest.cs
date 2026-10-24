using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Context;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.Upgrade.LevelInheritanceTestHelper;

namespace Tests.CombinedTest.Core.Machine.Upgrade.Regression
{
    public class SubstituteCapacityRegressionTest
    {
        [Test]
        public void LowerVariantCapacityCannotDestroyRetainedStockTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var container = ServerContext.GetService<MasterJsonFileContainer>();
            var key = new JsonFileName("items");
            var config = container.ConfigJsons.Single(entry => entry.JsonContents.ContainsKey(key));
            var json = JObject.Parse(config.JsonContents[key]);
            var lowerGuid = MasterHolder.ItemMaster.GetItemGuid(ForUnitTestLevelInheritanceIds.Source(1)).ToString();
            var lower = json["data"].Single(item => (string)item["itemGuid"] == lowerGuid);
            var table = (JObject)json["itemStackLevelTables"][0].DeepClone();
            var tableGuid = "f7804f00-5020-4860-8f4b-6b21d505ce1d";
            table["tableGuid"] = tableGuid;
            table["stackCounts"] = new JArray(1);
            ((JArray)json["itemStackLevelTables"]).Add(table);
            lower["stackLevelTableGuid"] = tableGuid;
            config.JsonContents[key] = json.ToString();
            MasterHolder.Load(container);
            var (block, inventory, _) = PlaceMachine(Vector3Int.zero);
            MachineRecipeSelectTestUtil.SelectRecipe(block, Recipe(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid));
            inventory.SetItem(0, Create(ForUnitTestLevelInheritanceIds.Source(2), 2));
            var incoming = Create(ForUnitTestLevelInheritanceIds.Source(1), 1);
            Assert.IsFalse(((IBlockInventory)inventory).InsertionCheck(new List<Core.Item.Interface.IItemStack> { incoming }));
            var remainder = inventory.InsertItem(incoming, InsertItemContext.Empty);
            Assert.AreEqual(incoming.Id, remainder.Id);
            Assert.AreEqual(1, remainder.Count);
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.Source(2), inventory.GetItem(0).Id);
            Assert.AreEqual(2, inventory.GetItem(0).Count);
        }
    }
}
