using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Tests.CombinedTest.Game.SaveLoad.Tier9
{
    /// <summary>
    ///     master 版7のゴールデンセーブと目印
    ///     The master version-7 golden save and its landmarks
    /// </summary>
    /// <remarks>
    ///     手書き JSON では形の食い違いに気づけないため、master e855a58667 の Editor で生成した。
    ///     テスト mod に無い本番 GUID の記録（研究完了8・ブロック解放3・表示解放8）だけを master の保存型で合成し、残りは実シリアライズ。
    ///     生成器と由来: moorestech_logs の harness/tier9-integration/g5-fixture/
    ///     Generated on the master e855a58667 Editor because hand-written JSON hides shape drift.
    ///     Only records for production GUIDs absent from the test mod (8 completions, 3 block and 8 item-view unlocks) were synthesized with master's save types; the rest is real serialization.
    ///     Generator and provenance: harness/tier9-integration/g5-fixture/ in moorestech_logs
    /// </remarks>
    public static class Tier9GoldenSaveFixture
    {
        public static readonly string[] ResetResearchGuids =
        {
            "019e3aab-32c6-7166-a184-d761153c2498", "019e3ae2-5005-7439-b242-9b7b3191a2c3", "019e3af5-f2e6-76de-a80f-5e66e79fa35d",
            "019e3af0-297f-726e-8a02-0a8518124e6d", "019e3aef-a149-746b-8fad-159e0434cb51", "01a16291-4094-7369-a87f-5dfe2197d25e",
        };
        public static readonly string[] ResetBlockGuids = { "01a162f1-ab04-76e2-94cf-dc713edeb63b", "01a162aa-cec0-738c-93c5-f58adc87fa7f", "01a162b0-7769-7419-af24-bb35528b93d2" };
        public static readonly string[] ResetItemViewGuids = { "01a162ab-07ba-73c1-9dd6-38e1e867663b", "019e3af5-e750-71ae-ab09-2b98f3db9913", "01a16290-f793-748b-a0b0-91d44be0d4ab", "01a16297-4724-73b9-82c3-2e77120be77d", "01a16297-6665-765d-8e19-96d357394c3e", "01a16297-a7f5-759b-91ba-be4021ca3bf2", "01a162a4-9334-7286-a8ba-7daf9c0f407a", "01a162a8-e435-73d0-bddb-08533819b80e" };
        public static readonly string[] KeptResearchGuids = { "cd05e30d-d599-46d3-a079-769113cbbf17", "c0de0000-0000-4000-8000-000000000011", "c0de0000-0000-4000-8000-000000000013" };

        public static readonly Vector3Int MachinePosition = new(0, 0, 0);
        public static readonly Vector3Int CleanRoomMachinePosition = new(10, 0, 0);
        public static readonly Vector3Int PipePosition = new(20, 0, 0);
        public const string VanillaMachineSaveKey = "Game.Block.Blocks.Machine.VanillaMachineSaveComponent";
        public const string CleanRoomMachineSaveKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";
        public const string PipeSaveKey = "Game.Block.Blocks.Fluid.FluidPipeSaveComponent";

        // 生成器が仕込んだ加工と流体の値
        // The craft and fluid values the generator set up
        private const string MachineRecipeGuid = "bd3d4d7d-9c3b-4ae1-875b-950327eedd9d";
        private const string Test3ItemGuid = "00000000-0000-0000-1234-000000000003";
        private const string PipeFluidGuid = "00000000-0000-0000-1234-000000000001";
        private const int ProcessingStateValue = 1;
        public const long PipeAmountNano = 5_000_000_000;
        private const double RecipeSeconds = 1.5;

        public static JObject Load()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            return JObject.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/Tier9/Fixtures/master-v7-tier9.json")));
        }

        // 座標のブロックの state を返す
        // Return the state of the block at a position
        public static JObject StateAt(JObject save, Vector3Int position, string saveKey)
        {
            var block = ((JArray)save["world"]).Single(token => token["X"].Value<int>() == position.x && token["Y"].Value<int>() == position.y && token["Z"].Value<int>() == position.z);
            Assert.IsTrue(block["state"]?[saveKey] is JObject, $"{position} に {saveKey} がありません");
            return (JObject)block["state"][saveKey];
        }

        private static bool HasUnlockedRecord(JObject save, string infosKey, string guid)
        {
            return ((JArray)save["gameUnlockState"][infosKey]).Any(token => token["guid"].Value<string>() == guid && token["isUnlocked"].Value<bool>());
        }

        // 削除の検査が空振りしないよう移行前の実態を正に確かめる
        // Assert the pre-migration contents so deletion checks cannot pass vacuously
        public static void AssertVersion7Preconditions(JObject original)
        {
            Assert.AreEqual(7, original["worldVersion"].Value<int>());
            var completed = original["research"]["CompletedResearchGuids"].Values<string>().ToList();
            CollectionAssert.IsSubsetOf(ResetResearchGuids, completed);
            CollectionAssert.IsSubsetOf(KeptResearchGuids, completed);
            foreach (var guid in ResetBlockGuids) Assert.IsTrue(HasUnlockedRecord(original, "blockUnlockStateInfos", guid), $"解放済みブロックの記録がありません {guid}");
            foreach (var guid in ResetItemViewGuids) Assert.IsTrue(HasUnlockedRecord(original, "itemUnlockStateInfos", guid), $"解放済み表示の記録がありません {guid}");

            // 機械は加工途中で継承レベル無し
            // The machine is mid-job without an inherited level
            var machine = StateAt(original, MachinePosition, VanillaMachineSaveKey);
            var processor = (JObject)machine["processor"];
            Assert.AreEqual(ProcessingStateValue, processor["state"].Value<int>());
            Assert.AreEqual(MachineRecipeGuid, processor["recipeGuid"].Value<string>());
            var remainingSeconds = processor["remainingSeconds"].Value<double>();
            Assert.That(remainingSeconds, Is.GreaterThan(0).And.LessThan(RecipeSeconds));
            var pending = (JArray)processor["pendingOutputs"];
            Assert.AreEqual(1, pending.Count);
            Assert.AreEqual(Test3ItemGuid, pending[0]["itemGuid"].Value<string>());
            Assert.AreEqual(1, pending[0]["count"].Value<int>());
            Assert.IsTrue(((JArray)machine["inputSlot"]).All(slot => slot["count"].Value<int>() == 0));
            Assert.IsFalse(processor.ContainsKey("inheritedLevel"));

            // 版8で足すキーはまだ無い
            // Keys added by version 8 are still absent
            var cleanRoom = StateAt(original, CleanRoomMachinePosition, CleanRoomMachineSaveKey);
            foreach (var key in new[] { "inputFluidSlot", "outputFluidSlot", "inheritedLevel" }) Assert.IsFalse(cleanRoom.ContainsKey(key), key);
            Assert.IsFalse(original.ContainsKey("rocketHasLaunchedOnce"));
            Assert.IsFalse(original.ContainsKey("spaceElevatorCablePool"));

            // パイプは流体を5個持つ
            // The pipe holds five fluid units
            var pipe = StateAt(original, PipePosition, PipeSaveKey);
            Assert.AreEqual(PipeFluidGuid, pipe["fluidGuid"].Value<string>());
            Assert.AreEqual(PipeAmountNano, pipe["amountNano"].Value<long>());
        }
    }
}
