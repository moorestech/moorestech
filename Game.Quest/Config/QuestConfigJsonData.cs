using Core.Util;
using Game.Quest.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Game.Quest.Config
{
    [JsonObject("SpaceAssets")]
    internal class QuestConfigJsonData
    {
        [JsonIgnore]
        public string ModId = null;
        
        
        
        [JsonProperty("Id")]
        private string Id;
        
        //クエストIDだけだと被るかもしれないのでmodIdとquestIdを結合する
        public string QuestId => ModId + ":" + Id; 
        
        [JsonProperty("Prerequisite")]
        public string[] Prerequisite;
        [JsonProperty("PrerequisiteType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public QuestPrerequisiteType PrerequisiteType;
        [JsonProperty("Category")]
        public string Category;
        [JsonProperty("Type")]
        public string Type;
        [JsonProperty("Name")]
        public string Name;
        [JsonProperty("Description")]
        public string Description;
        [JsonProperty("UIPosX")]
        public float UiPosX;
        [JsonProperty("UIPosY")]
        public float UiPosY;
        [JsonProperty("RewardItem")]
        public ItemJsonData[] RewardItem;
        [JsonProperty("Param")]
        public string Param;
    }
}