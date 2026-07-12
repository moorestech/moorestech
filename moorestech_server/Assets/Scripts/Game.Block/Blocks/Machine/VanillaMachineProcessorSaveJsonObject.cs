using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Machine
{
    public class VanillaMachineProcessorSaveJsonObject
    {
        [JsonProperty("state")]
        public int State;

        // 秒数として保存（tick数の変動に対応）
        // Save as seconds (to handle tick rate changes)
        [JsonProperty("remainingSeconds")]
        public double RemainingSeconds;

        [JsonProperty("recipeGuid")]
        public string RecipeGuidStr;

        [JsonIgnore]
        public Guid RecipeGuid => Guid.Parse(RecipeGuidStr);

        // 産出予定。Idle時や過去セーブではnull
        // Pending outputs; null while idle or in old saves
        [JsonProperty("pendingOutputs")]
        public List<ItemStackSaveJsonObject> PendingOutputs;

        // 選択中レシピ。未選択はnull（旧セーブもキー無し=null）
        // Selected recipe; null when unselected (older saves lack the key)
        [JsonProperty("selectedRecipeGuid")]
        public string SelectedRecipeGuidStr;
    }
}
