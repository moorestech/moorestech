using System;
using System.Linq;
using Core.Master;
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.CombinedTest.Core.Machine.Upgrade.Regression
{
    public class MachineUpgradeReviewRegressionTest
    {
        [Test]
        public void EqualStackLimitStillPublishesLevelChangeTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var container = ServerContext.GetService<MasterJsonFileContainer>();
            var key = new JsonFileName("items");
            var config = container.ConfigJsons.Single(entry => entry.JsonContents.ContainsKey(key));
            var json = JObject.Parse(config.JsonContents[key]);
            json["moduleSlotStackLimitLevels"][1]["stackLimit"] = json["moduleSlotStackLimitLevels"][0]["stackLimit"].DeepClone();
            config.JsonContents[key] = json.ToString();
            MasterHolder.Load(container);
            var lookup = ServerContext.GetService<IModuleSlotStackLimitLookup>();
            var notifications = 0;
            using var subscription = lookup.OnStackLimitChanged.Subscribe(_ => notifications++);
            var action = MasterHolder.ResearchMaster.GetResearch(ForUnitTestLevelInheritanceIds.ModuleSlotStackResearchGuid).ClearedActions.items[0];
            ServerContext.GetService<IModuleSlotStackLimitUnlocker>().ApplyUnlockModuleSlotStackLevelAction(action);
            Assert.AreEqual(1, lookup.CurrentLevel);
            Assert.AreEqual(3, lookup.CurrentStackLimit);
            Assert.AreEqual(1, notifications);
        }

        [Test]
        public void ShapeCacheDoesNotRetainPreviousMasterArraysTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestLevelInheritanceIds.InheritRecipeGuid);
            Assert.IsTrue(MachineRecipeLevelInheritanceUtil.TryGetShape(recipe, out var before));
            var container = ServerContext.GetService<MasterJsonFileContainer>();
            MasterHolder.Load(container);
            var reloadedRecipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(recipe.MachineRecipeGuid);
            Assert.AreNotSame(recipe, reloadedRecipe);
            Assert.IsTrue(MachineRecipeLevelInheritanceUtil.TryGetShape(reloadedRecipe, out var after));
            Assert.AreNotSame(before.SourceVariants, after.SourceVariants);
            CollectionAssert.AreEqual(before.SourceVariants, after.SourceVariants);
        }
    }
}
