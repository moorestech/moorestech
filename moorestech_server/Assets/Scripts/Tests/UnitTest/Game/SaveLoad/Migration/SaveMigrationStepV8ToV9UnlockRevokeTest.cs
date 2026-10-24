using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Game.SaveLoad.Migration.Steps.V8ToV9;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SaveMigrationStepV8ToV9UnlockRevokeTest
    {
        private const string SpaceElevatorBaseGuid = "01a162aa-cec0-738c-93c5-f58adc87fa7f";
        private const string RocketLaunchPadGuid = "01a162b0-7769-7419-af24-bb35528b93d2";
        private const string FusionReactorGuid = "01a162f1-ab04-76e2-94cf-dc713edeb63b";
        private const string OtherBlockGuid = "00000000-0000-0000-0000-000000000123";
        private const string FailureLog = "版8から版9へ変換できません";

        [Test]
        public void 解放済みの仮ブロック3種だけが未解放へ戻り他の解放は残るTest()
        {
            // 研究で3種を解放し終えた版8セーブの形（GUIDは大文字でも同じブロック）
            // A version-8 save whose research already unlocked the three (an upper-case GUID is the same block)
            var save = Save(Info(SpaceElevatorBaseGuid, true), Info(RocketLaunchPadGuid.ToUpperInvariant(), true), Info(FusionReactorGuid, true), Info(OtherBlockGuid, true));

            var result = new SaveMigrationStepV8ToV9().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var infos = (JArray)result.Save["gameUnlockState"]["blockUnlockStateInfos"];
            Assert.IsFalse(infos[0]["isUnlocked"].Value<bool>());
            Assert.IsFalse(infos[1]["isUnlocked"].Value<bool>());
            Assert.IsFalse(infos[2]["isUnlocked"].Value<bool>());
            Assert.IsTrue(infos[3]["isUnlocked"].Value<bool>());
            Assert.AreEqual(4, infos.Count);
            Assert.AreEqual(RocketLaunchPadGuid.ToUpperInvariant(), infos[1]["guid"].Value<string>());
        }

        [Test]
        public void 未解放の3種と解放記録の無いセーブは素通しされ再実行しても変わらないTest()
        {
            var lockedSave = Save(Info(FusionReactorGuid, false), Info(OtherBlockGuid, false));
            var lockedOriginal = lockedSave.DeepClone();
            var withoutInfos = new JObject { ["worldVersion"] = 8, ["world"] = new JArray(), ["gameUnlockState"] = new JObject() };
            var withoutInfosOriginal = withoutInfos.DeepClone();

            var locked = new SaveMigrationStepV8ToV9().Migrate(lockedSave);
            var noInfos = new SaveMigrationStepV8ToV9().Migrate(withoutInfos);

            Assert.IsTrue(locked.IsConverted, locked.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(lockedOriginal, locked.Save));
            Assert.IsTrue(noInfos.IsConverted, noInfos.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(withoutInfosOriginal, noInfos.Save));

            // 1回目で戻した結果は2回目で変わらない
            // A second run leaves the first run's result unchanged
            var first = new SaveMigrationStepV8ToV9().Migrate(Save(Info(FusionReactorGuid, true)));
            var once = first.Save.DeepClone();
            var second = new SaveMigrationStepV8ToV9().Migrate(first.Save);
            Assert.IsTrue(second.IsConverted, second.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(once, second.Save));
        }

        [Test]
        public void 対象GUIDは研究から外した3種と一致するTest()
        {
            CollectionAssert.AreEquivalent(
                new[] { SpaceElevatorBaseGuid, RocketLaunchPadGuid, FusionReactorGuid },
                new[] { UnimplementedBlockUnlockRevoker.RevokedBlockGuids[0].ToString(), UnimplementedBlockUnlockRevoker.RevokedBlockGuids[1].ToString(), UnimplementedBlockUnlockRevoker.RevokedBlockGuids[2].ToString() });
            Assert.AreEqual(3, UnimplementedBlockUnlockRevoker.RevokedBlockGuids.Count);
        }

        // 解放状態を辿れない形は理由つきで拒否し、清浄室機械の補填も含めて原本を変えない
        // Refuse an unwalkable unlock state with a reason and leave the original untouched, fluid filling included
        [TestCase("[]")]
        [TestCase("{\"blockUnlockStateInfos\":{}}")]
        [TestCase("{\"blockUnlockStateInfos\":[1]}")]
        public void 辿れない解放状態は理由つきで拒否するTest(string unlockStateJson)
        {
            var processorKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";
            var save = new JObject
            {
                ["worldVersion"] = 8,
                ["world"] = new JArray(new JObject { ["state"] = new JObject { [processorKey] = new JObject() } }),
                ["gameUnlockState"] = JToken.Parse(unlockStateJson),
            };
            var original = save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex(FailureLog));

            var result = new SaveMigrationStepV8ToV9().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            Assert.IsNotEmpty(result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(original, save));
        }

        #region TestHelper

        private static JObject Save(params JObject[] infos)
        {
            var unlockState = new JObject { ["blockUnlockStateInfos"] = new JArray(infos) };
            return new JObject { ["worldVersion"] = 8, ["world"] = new JArray(), ["gameUnlockState"] = unlockState };
        }

        private static JObject Info(string guid, bool isUnlocked)
        {
            return new JObject { ["guid"] = guid, ["isUnlocked"] = isUnlocked };
        }

        #endregion
    }
}
