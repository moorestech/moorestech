using System;
using System.Linq;
using System.Reflection;
using Core.Master;
using Game.SaveLoad.Json;
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Pruning;
using Microsoft.Extensions.DependencyInjection;
using Game.SaveLoad.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.CombinedTest.Game.MissingMasterPrune
{
    /// <summary>セーブの全節が除去器か除去不要宣言のどちらかに分類され、手書き一覧の足し忘れで素通りしないことを見る</summary>
    /// <summary>Checks every save section is classified as pruned or declared prune-free, so a forgotten entry cannot let one pass through</summary>
    public class MissingMasterSectionCoverageTest
    {
        private const string MissingGuid = SaveLoadPreparerTestFixture.MissingGuid;

        // 新しい節をWorldSaveAllInfoV1へ足したら、このテストが分類を迫る
        // Adding a new section to WorldSaveAllInfoV1 makes this test demand a classification
        [Test]
        public void セーブ形式の全節が除去器か除去不要宣言に分類されているTest()
        {
            var pruner = new MissingMasterPruner();
            var members = typeof(WorldSaveAllInfoV1).GetMembers(BindingFlags.Public | BindingFlags.Instance);
            var sectionNames = members.Select(member => member.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName).Where(name => name != null).ToList();

            Assert.Greater(sectionNames.Count, 0);
            CollectionAssert.IsEmpty(sectionNames.Where(name => !pruner.IsClassifiedSection(name)).ToList());
        }

        // ロード側がマスタに無いアイテム解放状態を読み飛ばすことを固定する。除去が漏れても起動は止まらない
        // Pins that the loader skips item unlock states absent from the master, so a missed prune still never blocks the boot
        [Test]
        public void マスタに無いアイテム解放状態は除去前のままでもロードが止まらないTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            ((JArray)save["gameUnlockState"]["itemUnlockStateInfos"]).Add(new JObject { ["guid"] = MissingGuid, ["isUnlocked"] = true });

            var loader = (WorldLoaderFromJson)SaveLoadPreparerTestFixture.CreateContainer().GetService<IWorldSaveDataLoader>();

            Assert.DoesNotThrow(() => loader.Load(save));
        }

        [Test]
        public void マスタに無いアイテムとブロックの解放状態は退避されるが通知件数に入らないTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            ((JArray)save["gameUnlockState"]["itemUnlockStateInfos"]).Add(new JObject { ["guid"] = MissingGuid, ["isUnlocked"] = true });
            ((JArray)save["gameUnlockState"]["blockUnlockStateInfos"]).Add(new JObject { ["guid"] = MissingGuid, ["isUnlocked"] = false });

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.IsFalse(outcome.Report.HasRemoval);
            Assert.IsTrue(outcome.HasRemoval);
            StringAssert.DoesNotContain(MissingGuid, outcome.Save["gameUnlockState"].ToString());
            var removed = (JArray)outcome.ToPrunedJson(DateTime.UtcNow)["unlockStates"];
            CollectionAssert.AreEquivalent(new[] { "itemUnlockStateInfos", "blockUnlockStateInfos" }, removed.Select(entry => entry["list"].Value<string>()));
        }

        // マスタに無い貨車を残すとTrainCar.RestoreTrainCarが例外を投げ、そのワールドは永久に起動不能になる
        // Leaving a car absent from the master makes TrainCar.RestoreTrainCar throw and that world can never load again
        [Test]
        public void マスタに無い貨車を含む列車は編成ごと除去され残った貨車の積荷は住所つきで空になるTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            var existingCarGuid = MasterHolder.TrainUnitMaster.Train.TrainCars[0].TrainCarGuid.ToString();
            var trainUnits = new JArray
            {
                TrainUnit(Car(11, MissingGuid, null)),
                TrainUnit(Car(22, existingCarGuid, ItemContainer(4))),
            };
            save["trainUnits"] = trainUnits;

            var outcome = new MissingMasterPruner().Prune(save);
            var pruned = outcome.ToPrunedJson(DateTime.UtcNow);

            Assert.AreEqual(1, ((JArray)outcome.Save["trainUnits"]).Count);
            Assert.AreEqual(1, ((JArray)pruned["trainUnits"]).Count);
            Assert.AreEqual(MissingGuid, pruned["trainUnits"][0]["Cars"][0]["TrainCarMasterId"].Value<string>());

            // 残った列車の積荷は通知件数に入り、貨車IDとスロットを住所に持つ
            // The remaining train's cargo counts toward the notice and carries the car id and slot as its address
            Assert.AreEqual(1, outcome.Report.EmptiedItemStackCount);
            var item = (JObject)((JArray)pruned["items"]).Single();
            Assert.AreEqual("trainUnits", item["origin"]["section"].Value<string>());
            Assert.AreEqual(22, item["origin"]["trainCarInstanceId"].Value<long>());
            Assert.AreEqual(0, item["origin"]["slot"].Value<int>());
            Assert.AreEqual(4, item["count"].Value<int>());
            StringAssert.DoesNotContain(MissingGuid, outcome.Save["trainUnits"].ToString());

            #region Internal

            JObject TrainUnit(JObject car)
            {
                return new JObject { ["TrainUnitInstanceId"] = Guid.NewGuid().ToString(), ["Cars"] = new JArray(car) };
            }

            JObject Car(long instanceId, string masterGuid, string containerSaveData)
            {
                return new JObject { ["TrainCarInstanceId"] = instanceId, ["TrainCarMasterId"] = masterGuid, ["ContainerSaveData"] = containerSaveData };
            }

            // TrainCarContainerSaveJsonObjectの形。containerStateはさらにJSON文字列で二重に埋め込まれる
            // The TrainCarContainerSaveJsonObject shape; containerState is embedded once more as a JSON string
            string ItemContainer(int count)
            {
                var stacks = new JArray(new JObject { ["itemGuid"] = MissingGuid, ["count"] = count });
                return new JObject { ["containerType"] = "Item", ["containerState"] = stacks.ToString(Formatting.None) }.ToString(Formatting.None);
            }

            #endregion
        }
    }
}
