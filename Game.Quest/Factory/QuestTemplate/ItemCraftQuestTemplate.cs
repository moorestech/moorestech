using Core.Item;
using Game.Quest.Interface;
using Game.Quest.QuestEntity;
using Newtonsoft.Json;

namespace Game.Quest.Factory.QuestTemplate
{
    public class ItemCraftQuestTemplate : IQuestTemplate
    {
        private readonly ItemStackFactory _itemStackFactory;

        public ItemCraftQuestTemplate(ItemStackFactory itemStackFactory)
        {
            _itemStackFactory = itemStackFactory;
        }

        public IQuest CreateQuest(QuestConfigData questConfig)
        {
            return new ItemCraftQuest(questConfig,GetCraftItem(questConfig.QuestParameter));
        }

        public IQuest LoadQuest(QuestConfigData questConfig, bool isCompleted, bool isRewarded)
        {
            return new ItemCraftQuest(questConfig,isCompleted,isRewarded,GetCraftItem(questConfig.QuestParameter));
        }

        private int GetCraftItem(string parameter)
        {
            var param = JsonConvert.DeserializeObject<ItemCraftQuestParameter>(parameter);
            return _itemStackFactory.Create(param.ModId, param.ItemName);
        }
    }
    

    [JsonObject("ItemCraftQuestParameter")]
    internal class ItemCraftQuestParameter
    {
        [JsonProperty("modId")]
        public string ModId;
        [JsonProperty("name")]
        public string ItemName;
    }
}