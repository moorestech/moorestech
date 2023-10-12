using Core.Util;
using Game.Quest.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Game.Quest.Config
{
    [JsonObject("SpaceAssets")]
    internal class QuestConfigJsonData
    {
        [JsonProperty("Category")] public string Category;

        [JsonProperty("Description")] public string Description;


        [JsonProperty("Id")] private string Id;

        [JsonIgnore] public string ModId;

        [JsonProperty("Name")] public string Name;

        [JsonProperty("Param")] public string Param;

        [JsonProperty("Prerequisite")] public string[] Prerequisite;

        [JsonProperty("PrerequisiteType")] [JsonConverter(typeof(StringEnumConverter))]
        public QuestPrerequisiteType PrerequisiteType;

        [JsonProperty("RewardItem")] public ItemJsonData[] RewardItem;

        [JsonProperty("Type")] public string Type;

        [JsonProperty("UIPosX")] public float UiPosX;

        [JsonProperty("UIPosY")] public float UiPosY;

        //クエストIDだけだと被るかもしれないのでmodIdとquestIdを結合する
        public string QuestId => ModId + ":" + Id;
    }
}