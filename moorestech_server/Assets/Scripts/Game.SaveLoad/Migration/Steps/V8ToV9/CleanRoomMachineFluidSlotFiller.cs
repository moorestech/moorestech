using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V8ToV9
{
    /// <summary>
    /// 版8の清浄室機械stateへ空の液体スロット配列を足す（冪等）。版8以前の清浄室機械はタンクを持たなかった
    /// Add empty fluid slot arrays to version-8 clean-room machine states (idempotent); machines before version 9 had no tanks
    /// </summary>
    public static class CleanRoomMachineFluidSlotFiller
    {
        // 清浄室機械ProcessorのSaveKey(typeof FullName)。ステップは現行の型を参照しないので文字列で持つ
        // The clean-room processor's SaveKey (typeof FullName); the step never references live types, so it is kept as a string
        private const string ProcessorSaveKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";
        private const string InputFluidSlotKey = "inputFluidSlot";
        private const string OutputFluidSlotKey = "outputFluidSlot";

        // 書き換える前に全件を検査し、辿れない形なら理由を返して何も変えない
        // Inspect every block before rewriting; on an unwalkable shape return the reason and change nothing
        public static bool TryCollectTargets(JArray world, List<JObject> targets, out string failureReason)
        {
            foreach (var blockToken in world)
            {
                if (!(blockToken is JObject block)) return Reject($"world要素がオブジェクトではありません。 type={blockToken.Type}", out failureReason);
                var stateToken = block["state"];
                if (stateToken == null || stateToken.Type == JTokenType.Null) continue;
                if (!(stateToken is JObject state)) return Reject($"world要素のstateがオブジェクトではありません。 type={stateToken.Type}", out failureReason);

                // 清浄室機械以外はキーが無いので対象外
                // Blocks other than clean-room machines lack the key and are out of scope
                var processorToken = state[ProcessorSaveKey];
                if (processorToken == null) continue;
                if (!(processorToken is JObject processorState)) return Reject($"{ProcessorSaveKey} がオブジェクトではありません。 type={processorToken.Type}", out failureReason);
                if (!IsArrayOrAbsent(processorState[InputFluidSlotKey])) return Reject($"{InputFluidSlotKey} が配列ではありません。 type={processorState[InputFluidSlotKey].Type}", out failureReason);
                if (!IsArrayOrAbsent(processorState[OutputFluidSlotKey])) return Reject($"{OutputFluidSlotKey} が配列ではありません。 type={processorState[OutputFluidSlotKey].Type}", out failureReason);
                targets.Add(processorState);
            }

            failureReason = null;
            return true;

            #region Internal

            bool IsArrayOrAbsent(JToken token)
            {
                return token == null || token is JArray;
            }

            #endregion
        }

        // 欠けている液体キーだけ空配列で足し、足した台数を返す（再実行しても変わらない）
        // Add empty arrays only where a fluid key is missing and return the count (idempotent on rerun)
        public static int Fill(IReadOnlyList<JObject> targets)
        {
            var filledCount = 0;
            foreach (var processorState in targets)
            {
                if (processorState[InputFluidSlotKey] != null && processorState[OutputFluidSlotKey] != null) continue;
                if (processorState[InputFluidSlotKey] == null) processorState[InputFluidSlotKey] = new JArray();
                if (processorState[OutputFluidSlotKey] == null) processorState[OutputFluidSlotKey] = new JArray();
                filledCount++;
            }

            return filledCount;
        }

        private static bool Reject(string reason, out string failureReason)
        {
            failureReason = reason;
            return false;
        }
    }
}
