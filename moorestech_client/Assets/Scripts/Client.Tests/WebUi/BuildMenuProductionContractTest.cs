using System;
using System.Collections.Generic;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics.BuildMenu;
using Core.Master;
using Mooresmaster.Model.BuildMenuModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.WebUi
{
    public class BuildMenuProductionContractTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void ProductionEntriesPreserveIdentityAndOnlyRequiredLabels()
        {
            // 実masterから型別entryを構築
            // Build typed entries from real master identities
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId);
            var trainCarGuid = Guid.Parse("ABCDEF03-2345-4678-9ABC-DEF012345678");
            var connectToolGuid = Guid.Parse("ABCDEF04-2345-4678-9ABC-DEF012345678");
            var entries = new[]
            {
                WebBuildMenuEntry.CreateBlock(ForUnitTestModBlockId.BlockId, blockMaster, EmptyItems()),
                WebBuildMenuEntry.CreateTrainCar(trainCarGuid, "Cargo Car", EmptyItems()),
                WebBuildMenuEntry.CreateConnectTool(connectToolGuid, "Wire Tool", EmptyItems()),
                WebBuildMenuEntry.CreateBlueprintCopy(EmptyItems()),
                WebBuildMenuEntry.CreateBlueprint("starter-base", EmptyItems()),
            };

            // 本番変換でwire形状を取得
            // Obtain the wire shape through production conversion
            var serialized = JArray.Parse(WebUiJson.Serialize(Array.ConvertAll(
                entries,
                BuildMenuEntryDtoFactory.CreateDto)));
            var blockCategory = MasterHolder.BuildMenuCategoryMaster.GetGuidPair(blockMaster.Category, blockMaster.SubCategory);
            var trainCategory = MasterHolder.BuildMenuCategoryMaster.GetPairByEntrySource(BuildMenuSubCategoryElement.EntrySourceConst.trainCars);
            var connectCategory = MasterHolder.BuildMenuCategoryMaster.GetPairByEntrySource(BuildMenuSubCategoryElement.EntrySourceConst.connectTools);
            var copyCategory = MasterHolder.BuildMenuCategoryMaster.GetPairByEntrySource(BuildMenuSubCategoryElement.EntrySourceConst.blueprintCopyTool);
            var blueprintCategory = MasterHolder.BuildMenuCategoryMaster.GetPairByEntrySource(BuildMenuSubCategoryElement.EntrySourceConst.savedBlueprints);

            // Guidと種別別label省略を固定
            // Pin GUIDs and per-type label omission
            Assert.AreEqual(blockMaster.BlockGuid.ToString("D"), serialized[0]!["entryKey"]!.Value<string>());
            Assert.IsNull(serialized[0]!["label"]);
            Assert.AreEqual("Cargo Car", serialized[1]!["label"]!.Value<string>());
            Assert.AreEqual("Wire Tool", serialized[2]!["label"]!.Value<string>());
            Assert.IsNull(serialized[3]!["label"]);
            Assert.AreEqual("starter-base", serialized[4]!["label"]!.Value<string>());
            AssertCategory(serialized[0]!, blockCategory);
            AssertCategory(serialized[1]!, trainCategory);
            AssertCategory(serialized[2]!, connectCategory);
            AssertCategory(serialized[3]!, copyCategory);
            AssertCategory(serialized[4]!, blueprintCategory);

            // 配信identityも選択照合を通す
            // Pass delivered identities through the selection matcher
            for (var index = 0; index < entries.Length; index++)
            {
                var dto = BuildMenuEntryDtoFactory.CreateDto(entries[index]);
                Assert.IsTrue(BuildMenuEntryDtoFactory.MatchesIdentity(entries[index], dto.EntryType, dto.EntryKey));
                Assert.IsFalse(BuildMenuEntryDtoFactory.MatchesIdentity(entries[index], dto.EntryType, dto.EntryKey + "-changed"));
            }
        }

        [Test]
        public void ProductionCategoryDtosPreserveMasterGuidOrder()
        {
            // 配信順とGuidを実masterへ対応
            // Match delivery order and GUIDs to the real master
            var actual = BuildMenuEntryDtoFactory.CreateCategoryDtos();
            var expected = MasterHolder.BuildMenuCategoryMaster.Categories;

            Assert.AreEqual(expected.Length, actual.Count);
            for (var categoryIndex = 0; categoryIndex < expected.Length; categoryIndex++)
            {
                Assert.AreEqual(expected[categoryIndex].CategoryGuid.ToString("D"), actual[categoryIndex].CategoryGuid);
                CollectionAssert.AreEqual(
                    Array.ConvertAll(expected[categoryIndex].SubCategories, value => value.SubCategoryGuid.ToString("D")),
                    actual[categoryIndex].SubCategoryGuids);
            }
        }

        private static IReadOnlyList<WebBuildMenuEntry.RequiredItem> EmptyItems()
        {
            return new List<WebBuildMenuEntry.RequiredItem>();
        }

        private static void AssertCategory(JToken serialized, (Guid categoryGuid, Guid subCategoryGuid) expected)
        {
            Assert.AreEqual(expected.categoryGuid.ToString("D"), serialized["categoryGuid"]!.Value<string>());
            Assert.AreEqual(expected.subCategoryGuid.ToString("D"), serialized["subCategoryGuid"]!.Value<string>());
        }
    }
}
