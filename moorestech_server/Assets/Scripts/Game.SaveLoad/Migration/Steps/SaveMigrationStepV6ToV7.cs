using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // V6→V7移行: 通常機械とクリーンルーム機械の加工状態に継承レベル null を明示で足す（版6に継承レシピは無いため「継承しない」が正しい）
    // V6→V7 migration: add an explicit null inherited level to normal and clean-room machine processing state (version 6 had no inheritance recipes, so "no inheritance" is exact)
    public sealed class SaveMigrationStepV6ToV7 : ISaveMigrationStep
    {
        // コンポーネントのSaveKey(typeof FullName)。ステップは現行の型を参照しないので文字列で持つ
        // Component SaveKeys (typeof FullName); the step never references live types, so they are kept as strings
        private const string VanillaMachineSaveKey = "Game.Block.Blocks.Machine.VanillaMachineSaveComponent";
        private const string CleanRoomMachineSaveKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";
        private const string InheritedLevelKey = "inheritedLevel";

        public int FromVersion => 6;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            if (!(save["world"] is JArray world)) return Fail($"セーブのworldが配列ではないため変換できません。 type={save["world"]?.Type}");

            var vanillaCount = 0;
            var cleanRoomCount = 0;
            foreach (var blockToken in world)
            {
                if (!(blockToken is JObject block)) return Fail($"world要素がオブジェクトではないため変換できません。 type={blockToken.Type}");

                // stateが無いブロックは機械ではないので対象外
                // A block without state is not a machine, so it is out of scope
                var stateToken = block["state"];
                if (stateToken == null || stateToken.Type == JTokenType.Null) continue;
                if (!(stateToken is JObject state)) return Fail($"world要素のstateがオブジェクトではありません。 type={stateToken.Type}");

                // 通常機械はprocessorサブオブジェクトへ足す
                // A normal machine gets the key inside its processor sub-object
                var vanillaToken = state[VanillaMachineSaveKey];
                if (vanillaToken != null && vanillaToken.Type != JTokenType.Null)
                {
                    if (!(vanillaToken is JObject vanilla) || !(vanilla["processor"] is JObject processor)) return Fail($"{VanillaMachineSaveKey} の processor がオブジェクトではありません。 type={vanillaToken["processor"]?.Type}");
                    if (!processor.ContainsKey(InheritedLevelKey))
                    {
                        processor[InheritedLevelKey] = JValue.CreateNull();
                        vanillaCount++;
                    }
                }

                // クリーンルーム機械は直下へ足す
                // A clean-room machine gets the key at its top level
                var cleanRoomToken = state[CleanRoomMachineSaveKey];
                if (cleanRoomToken != null && cleanRoomToken.Type != JTokenType.Null)
                {
                    if (!(cleanRoomToken is JObject cleanRoom)) return Fail($"{CleanRoomMachineSaveKey} がオブジェクトではありません。 type={cleanRoomToken.Type}");
                    if (!cleanRoom.ContainsKey(InheritedLevelKey))
                    {
                        cleanRoom[InheritedLevelKey] = JValue.CreateNull();
                        cleanRoomCount++;
                    }
                }
            }

            Debug.Log($"セーブを版6から版7へ変換しました。継承レベルを足した通常機械={vanillaCount}台 クリーンルーム機械={cleanRoomCount}台");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            SaveMigrationStepResult Fail(string failureReason)
            {
                // 直接実行した場合も拒否理由を残す
                // Preserve the refusal reason even when the step is called directly
                Debug.LogWarning($"セーブを版6から版7へ変換できません: {failureReason}");
                return SaveMigrationStepResult.Failed(failureReason);
            }

            #endregion
        }
    }
}
