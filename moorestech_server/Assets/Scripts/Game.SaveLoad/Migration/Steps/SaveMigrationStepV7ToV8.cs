using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps
{
    // V7→V8移行: 清浄室機械のstateへ空の液体スロット配列を足す。版7以前の清浄室機械はタンクを持たなかった
    // V7->V8 migration: add empty fluid slot arrays to clean-room machine states; machines before version 8 had no tanks
    public sealed class SaveMigrationStepV7ToV8 : ISaveMigrationStep
    {
        // 清浄室機械ProcessorのSaveKey(typeof FullName)。ステップは現行の型を参照しないので文字列で持つ
        // The clean-room processor's SaveKey (typeof FullName); the step never references live types, so it is kept as a string
        private const string ProcessorSaveKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";
        private const string InputFluidSlotKey = "inputFluidSlot";
        private const string OutputFluidSlotKey = "outputFluidSlot";

        public int FromVersion => 7;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            if (!(save["world"] is JArray world)) return Fail($"セーブのworldが配列ではないため変換できません。 type={save["world"]?.Type}");

            // 先に全件を検査し、途中で拒否しても原本を書き換えない
            // Inspect every block first so a refusal never leaves the original half-rewritten
            var targets = new List<JObject>();
            foreach (var blockToken in world)
            {
                if (!(blockToken is JObject block)) return Fail($"world要素がオブジェクトではありません。 type={blockToken.Type}");
                var stateToken = block["state"];
                if (stateToken == null || stateToken.Type == JTokenType.Null) continue;
                if (!(stateToken is JObject state)) return Fail($"world要素のstateがオブジェクトではありません。 type={stateToken.Type}");

                // 清浄室機械以外はキーが無いので対象外
                // Blocks other than clean-room machines lack the key and are out of scope
                var processorToken = state[ProcessorSaveKey];
                if (processorToken == null) continue;
                if (!(processorToken is JObject processorState)) return Fail($"{ProcessorSaveKey} がオブジェクトではありません。 type={processorToken.Type}");
                if (!IsArrayOrAbsent(processorState[InputFluidSlotKey])) return Fail($"{InputFluidSlotKey} が配列ではありません。 type={processorState[InputFluidSlotKey].Type}");
                if (!IsArrayOrAbsent(processorState[OutputFluidSlotKey])) return Fail($"{OutputFluidSlotKey} が配列ではありません。 type={processorState[OutputFluidSlotKey].Type}");
                targets.Add(processorState);
            }

            // 欠けている液体キーだけ空配列で足す（再実行しても変わらない）
            // Add empty arrays only where a fluid key is missing (idempotent on rerun)
            var migratedCount = 0;
            foreach (var processorState in targets)
            {
                if (processorState[InputFluidSlotKey] != null && processorState[OutputFluidSlotKey] != null) continue;
                if (processorState[InputFluidSlotKey] == null) processorState[InputFluidSlotKey] = new JArray();
                if (processorState[OutputFluidSlotKey] == null) processorState[OutputFluidSlotKey] = new JArray();
                migratedCount++;
            }

            Debug.Log($"セーブを版7から版8へ変換しました。液体スロットを足した清浄室機械={migratedCount}台");
            return SaveMigrationStepResult.Converted(save);

            #region Internal

            bool IsArrayOrAbsent(JToken token)
            {
                return token == null || token is JArray;
            }

            SaveMigrationStepResult Fail(string failureReason)
            {
                // 直接実行した場合も拒否理由を残す
                // Preserve the refusal reason even when the step is called directly
                Debug.LogWarning($"セーブを版7から版8へ変換できません: {failureReason}");
                return SaveMigrationStepResult.Failed(failureReason);
            }

            #endregion
        }
    }
}
