using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V7ToV8
{
    /// <summary>
    /// 清浄室機械の state へ空の液体スロット配列を足す（冪等）。版7以前の清浄室機械はタンクを持たなかった
    /// Add empty fluid slot arrays to clean-room machine states (idempotent); machines up to version 7 had no tanks
    /// </summary>
    public sealed class CleanRoomMachineFluidSlotFiller : IV7ToV8MigrationPart
    {
        private const string ProcessorSaveKey = V7ToV8SaveJson.CleanRoomMachineSaveKey;
        private const string InputFluidSlotKey = "inputFluidSlot";
        private const string OutputFluidSlotKey = "outputFluidSlot";

        private readonly List<JObject> _targets = new();
        private int _nullProcessorCount;

        public bool TryPrepare(JObject save, out string failureReason)
        {
            // world→ブロック→state の走査と拒否文は全部品で共有する
            // The world->block->state walk and its refusal texts are shared by every part
            if (!V7ToV8SaveJson.TryCollectBlockStates(save, out var states, out failureReason)) return false;

            foreach (var state in states)
            {
                // 清浄室機械以外はキーが無いので対象外
                // Blocks other than clean-room machines lack the key and are out of scope
                var processorToken = state[ProcessorSaveKey];
                if (processorToken == null) continue;

                // 明示nullはロード側も警告して復元を見送る形なので、止めずに台数だけ残す
                // An explicit null is also skipped with a warning by the loader, so count it instead of refusing
                if (V7ToV8SaveJson.IsExplicitNull(processorToken))
                {
                    _nullProcessorCount++;
                    continue;
                }

                if (!(processorToken is JObject processorState)) return V7ToV8SaveJson.Reject($"{ProcessorSaveKey} がオブジェクトではありません。 type={processorToken.Type}", out failureReason);
                var inputToken = processorState[InputFluidSlotKey];
                var outputToken = processorState[OutputFluidSlotKey];
                // 液体キーの明示nullは欠損と同じく空配列で補う（判断記録 A3）
                // An explicit null fluid key is filled like a missing one (decision A3)
                var inputMissing = V7ToV8SaveJson.IsAbsentOrNull(inputToken);
                var outputMissing = V7ToV8SaveJson.IsAbsentOrNull(outputToken);
                if (!inputMissing && !(inputToken is JArray)) return V7ToV8SaveJson.Reject($"{InputFluidSlotKey} が配列ではありません。 type={inputToken.Type}", out failureReason);
                if (!outputMissing && !(outputToken is JArray)) return V7ToV8SaveJson.Reject($"{OutputFluidSlotKey} が配列ではありません。 type={outputToken.Type}", out failureReason);
                if (inputMissing || outputMissing) _targets.Add(processorState);
            }

            return true;
        }

        public string ApplyAndSummarize()
        {
            // 欠けている液体キーだけ空配列で足す
            // Add empty arrays only where a fluid key is missing
            foreach (var processorState in _targets)
            {
                if (V7ToV8SaveJson.IsAbsentOrNull(processorState[InputFluidSlotKey])) processorState[InputFluidSlotKey] = new JArray();
                if (V7ToV8SaveJson.IsAbsentOrNull(processorState[OutputFluidSlotKey])) processorState[OutputFluidSlotKey] = new JArray();
            }

            return $"液体スロットを足した清浄室機械={_targets.Count}台 processorが null で飛ばした清浄室機械={_nullProcessorCount}台";
        }
    }
}
