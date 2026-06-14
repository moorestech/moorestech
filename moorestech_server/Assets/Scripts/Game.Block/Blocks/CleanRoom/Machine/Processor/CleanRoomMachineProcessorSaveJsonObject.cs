using System;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom
{
    // プロセッサのセーブ用 DTO。抽選の決定性を保つ _processedCycleCount を含めて永続化する。
    // Save DTO for the processor; persists _processedCycleCount so the lottery stays deterministic across save/load.
    public class CleanRoomMachineProcessorSaveJsonObject
    {
        [JsonProperty("state")]
        public int State;

        [JsonProperty("remainingSeconds")]
        public double RemainingSeconds;

        [JsonProperty("recipeGuid")]
        public string RecipeGuidStr;

        [JsonIgnore]
        public Guid RecipeGuid => Guid.Parse(RecipeGuidStr);

        // 抽選の決定性を保つサイクルカウンタ
        // Cycle counter that keeps the lottery deterministic
        [JsonProperty("processedCycleCount")]
        public uint ProcessedCycleCount;

        // プロセッサの現在値からセーブ DTO を組み立てる。
        // Build the save DTO from the processor's current values.
        public static CleanRoomMachineProcessorSaveJsonObject Create(int state, double remainingSeconds, string recipeGuidStr, uint processedCycleCount)
        {
            return new CleanRoomMachineProcessorSaveJsonObject
            {
                State = state,
                RemainingSeconds = remainingSeconds,
                RecipeGuidStr = recipeGuidStr,
                ProcessedCycleCount = processedCycleCount,
            };
        }
    }
}
